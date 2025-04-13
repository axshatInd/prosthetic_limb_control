import cv2
import mediapipe as mp
import socket
import json

# Initialize MediaPipe hands
mp_hands = mp.solutions.hands
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles

hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.6,
    min_tracking_confidence=0.5,
    model_complexity=1
)

UDP_IP = "127.0.0.1"
UDP_PORT = 6000
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

cap = cv2.VideoCapture(0)

def process_landmarks(landmarks):
    """Process landmarks with enhanced depth handling"""
    wrist_z = landmarks[0].z
    processed_landmarks = []
    
    for i, lm in enumerate(landmarks):
        # Calculate relative depth with massively increased sensitivity
        depth_value = -(lm.z - wrist_z) * 12.0  # Increased from 8.0 to 12.0
        
        # Additional processing for finger joints
        if i > 0:
            # Enhance depth differences at fingertips even more
            if i in [4, 8, 12, 16, 20]:  # Fingertips
                depth_value *= 2.0  # Increased from 1.5 to 2.0
                
        processed_landmarks.append({
            "x": lm.x,
            "y": lm.y,
            "z": depth_value
        })
    
    return processed_landmarks

while cap.isOpened():
    success, image = cap.read()
    if not success: continue
    
    image = cv2.flip(image, 1)
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    results = hands.process(image_rgb)
    
    if results.multi_hand_landmarks:
        hand_landmarks = results.multi_hand_landmarks[0]
        
        # Draw landmarks for visual feedback
        mp_drawing.draw_landmarks(
            image,
            hand_landmarks,
            mp_hands.HAND_CONNECTIONS,
            mp_drawing_styles.get_default_hand_landmarks_style(),
            mp_drawing_styles.get_default_hand_connections_style()
        )
        
        # Process landmarks for Unity
        landmark_list = process_landmarks(hand_landmarks.landmark)
        
        try:
            sock.sendto(json.dumps({"landmark": landmark_list}).encode(), (UDP_IP, UDP_PORT))
        except Exception as e:
            print(f"Send error: {e}")
    
    cv2.imshow("Hand Tracking", image)
    if cv2.waitKey(5) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
sock.close()
