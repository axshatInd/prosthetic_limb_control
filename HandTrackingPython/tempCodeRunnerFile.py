import cv2
import mediapipe as mp
import socket
import json
import time
import numpy as np

# Initialize MediaPipe Hand Solution with improved detection parameters
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.6,  # Increased for more stable detection
    min_tracking_confidence=0.6,   # Increased for more stable tracking
    model_complexity=1             # Using more accurate model (0, 1, or 2)
)
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles

# Initialize UDP communication
UDP_IP = "127.0.0.1"  # Localhost (same machine)
UDP_PORT = 6000
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# Start webcam capture
cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

# Tracking state variables
prev_frame_time = 0
smoothing_factor = 0.8  # Higher = more smoothing (0-1)
previous_landmarks = None
stabilize_counter = 0
stable_hand = False

# Filtering parameters
filter_window_size = 8  # Increased for more stability
landmark_history = []

# Depth normalization
depth_min = float('inf')
depth_max = float('-inf')
depth_history = []
depth_history_size = 30

print("Starting hand tracking. Press 'q' to quit, 's' to toggle smoothing.")

# Optional: Set up logging
import logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger('hand_tracker')

def smooth_landmarks(current, previous, factor):
    """Apply temporal smoothing to landmarks"""
    if previous is None:
        return current
    
    smooth_landmarks = []
    for i, landmark in enumerate(current):
        smooth_landmark = {
            "x": previous[i]["x"] * factor + landmark["x"] * (1 - factor),
            "y": previous[i]["y"] * factor + landmark["y"] * (1 - factor),
            "z": previous[i]["z"] * factor + landmark["z"] * (1 - factor)
        }
        smooth_landmarks.append(smooth_landmark)
    
    return smooth_landmarks

def median_filter(history, current):
    """Apply median filtering to landmarks"""
    if len(history) == 0:
        return current
        
    filtered_landmarks = []
    for i in range(len(current)):
        # Collect x, y, z values for this landmark across history
        x_values = [frame[i]["x"] for frame in history] + [current[i]["x"]]
        y_values = [frame[i]["y"] for frame in history] + [current[i]["y"]]
        z_values = [frame[i]["z"] for frame in history] + [current[i]["z"]]
        
        # Calculate median values
        filtered_landmarks.append({
            "x": sorted(x_values)[len(x_values)//2],
            "y": sorted(y_values)[len(y_values)//2],
            "z": sorted(z_values)[len(z_values)//2]
        })
    
    return filtered_landmarks

def normalize_depth(landmarks):
    """Normalize depth values to make them more consistent"""
    global depth_min, depth_max, depth_history
    
    # Extract all z values
    z_values = [landmark["z"] for landmark in landmarks]
    
    # Update depth history
    depth_history.append(z_values)
    if len(depth_history) > depth_history_size:
        depth_history.pop(0)
    
    # Calculate min and max depth across history
    all_z_values = [z for frame in depth_history for z in frame]
    if all_z_values:
        current_min = min(all_z_values)
        current_max = max(all_z_values)
        
        # Update global min/max with smoothing
        depth_min = min(depth_min, current_min) if depth_min != float('inf') else current_min
        depth_max = max(depth_max, current_max) if depth_max != float('-inf') else current_max
    
    # Ensure we have a reasonable range
    depth_range = max(0.01, depth_max - depth_min)
    
    # Normalize z values
    for landmark in landmarks:
        # Normalize to 0-1 range
        normalized_z = (landmark["z"] - depth_min) / depth_range
        # Scale to a reasonable range for Unity
        landmark["z"] = normalized_z * 0.2 - 0.1  # Range from -0.1 to 0.1
    
    return landmarks

def check_hand_stable(landmarks, threshold=0.01):
    """Check if hand position is stable"""
    if previous_landmarks is None:
        return False
        
    # Calculate total movement
    total_movement = 0
    for i, landmark in enumerate(landmarks):
        prev_pos = np.array([previous_landmarks[i]["x"], previous_landmarks[i]["y"], previous_landmarks[i]["z"]])
        curr_pos = np.array([landmark["x"], landmark["y"], landmark["z"]])
        total_movement += np.linalg.norm(curr_pos - prev_pos)
    
    avg_movement = total_movement / len(landmarks)
    return avg_movement < threshold

def preprocess_landmarks(landmarks):
    """Apply preprocessing to landmarks to make them more suitable for hand tracking"""
    # Center the hand around the wrist
    wrist_x = landmarks[0]["x"]
    wrist_y = landmarks[0]["y"]
    wrist_z = landmarks[0]["z"]
    
    # Center and scale
    for landmark in landmarks:
        # Center around wrist
        landmark["x"] = (landmark["x"] - wrist_x) * 2.0 + 0.5
        landmark["y"] = (landmark["y"] - wrist_y) * 2.0 + 0.5
        landmark["z"] = (landmark["z"] - wrist_z) * 5.0  # Exaggerate depth
    
    return landmarks

while cap.isOpened():
    success, image = cap.read()
    if not success:
        logger.warning("Failed to capture image from camera")
        continue
        
    # Calculate FPS
    current_time = time.time()
    fps = 1/(current_time - prev_frame_time) if prev_frame_time > 0 else 0
    prev_frame_time = current_time
    
    # Display FPS and status
    cv2.putText(image, f"FPS: {int(fps)}", (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 
                0.7, (0, 255, 0), 2, cv2.LINE_AA)
    
    cv2.putText(image, f"Smoothing: {'ON' if smoothing_factor > 0.5 else 'OFF'}", 
                (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.7, 
                (0, 255, 0) if smoothing_factor > 0.5 else (0, 0, 255), 2, cv2.LINE_AA)
    
    cv2.putText(image, f"Hand {'Stable' if stable_hand else 'Moving'}", 
                (10, 90), cv2.FONT_HERSHEY_SIMPLEX, 0.7, 
                (0, 255, 0) if stable_hand else (0, 165, 255), 2, cv2.LINE_AA)

    # Flip the image horizontally for a later selfie-view display
    image = cv2.flip(image, 1)
    
    # Convert to RGB for MediaPipe
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    image_rgb.flags.writeable = False
    results = hands.process(image_rgb)
    
    # Draw hand landmarks on the image
    image.flags.writeable = True
    image = cv2.cvtColor(image_rgb, cv2.COLOR_RGB2BGR)
    
    if results.multi_hand_landmarks:
        for hand_landmarks in results.multi_hand_landmarks:
            # Draw the landmarks with additional visual styling
            mp_drawing.draw_landmarks(
                image,
                hand_landmarks,
                mp_hands.HAND_CONNECTIONS,
                mp_drawing_styles.get_default_hand_landmarks_style(),
                mp_drawing_styles.get_default_hand_connections_style()
            )
            
            # Extract landmarks into a list of dictionaries
            landmark_list = []
            for landmark in hand_landmarks.landmark:
                landmark_list.append({
                    "x": landmark.x,
                    "y": landmark.y,
                    "z": landmark.z
                })
            
            # Preprocess landmarks to make them more suitable for hand tracking
            landmark_list = preprocess_landmarks(landmark_list)
            
            # Apply temporal smoothing if enabled
            if smoothing_factor > 0.5 and previous_landmarks is not None:
                landmark_list = smooth_landmarks(landmark_list, previous_landmarks, smoothing_factor)
            
            # Apply median filtering if we have enough history
            if len(landmark_history) > 0:
                landmark_list = median_filter(landmark_history, landmark_list)
            
            # Normalize depth values
            landmark_list = normalize_depth(landmark_list)
            
            # Update history for median filtering
            landmark_history.append(landmark_list.copy())
            if len(landmark_history) > filter_window_size:
                landmark_history.pop(0)
            
            # Check if hand is stable
            is_stable_now = check_hand_stable(landmark_list)
            
            # Update stability counter for debouncing
            if is_stable_now:
                stabilize_counter += 1
            else:
                stabilize_counter = max(0, stabilize_counter - 1)
            
            # Hand is considered stable after several consecutive stable frames
            stable_hand = stabilize_counter > 5
            
            # Store current landmarks for next frame comparison
            previous_landmarks = landmark_list.copy()
            
            # Format data for Unity
            hand_data = {
                "landmark": landmark_list
            }
            
            # Convert to JSON and send via UDP
            try:
                json_data = json.dumps(hand_data)
                sock.sendto(json_data.encode('utf-8'), (UDP_IP, UDP_PORT))
                
                # Log occasionally
                if int(time.time()) % 5 == 0 and int(time.time() * 10) % 10 == 0:
                    logger.info(f"Hand detected, stability: {stable_hand}")
            except Exception as e:
                logger.error(f"Error sending data: {e}")
    else:
        # No hands detected
        previous_landmarks = None
        stable_hand = False
        stabilize_counter = 0
        landmark_history = []
    
    # Display the image
    cv2.imshow("MediaPipe Hand Tracking", image)
    
    # Check for key presses
    key = cv2.waitKey(5) & 0xFF
    if key == ord('q'):
        break
    elif key == ord('s'):
        # Toggle smoothing intensity
        if smoothing_factor > 0.5:
            smoothing_factor = 0.3  # Less smoothing
        else:
            smoothing_factor = 0.8  # More smoothing
        logger.info(f"Smoothing set to {smoothing_factor}")

# Clean up
cap.release()
cv2.destroyAllWindows()
sock.close()
print("Hand tracking stopped")