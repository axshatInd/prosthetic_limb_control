import cv2
import mediapipe as mp
import socket
import json

# Initialize MediaPipe Hand Solution
mp_hands = mp.solutions.hands
hands = mp_hands.Hands(min_detection_confidence=0.7, min_tracking_confidence=0.5)
mp_drawing = mp.solutions.drawing_utils

# Initialize UDP communication
UDP_IP = "127.0.0.1"  # Localhost (same machine)
UDP_PORT = 5005
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# Start webcam capture
cap = cv2.VideoCapture(0)

while True:
    success, image = cap.read()
    if not success:
        break

    # Flip the image horizontally for a later selfie-view display
    image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    image.flags.writeable = False

    # Process the image and get hand landmarks
    results = hands.process(image)
    image.flags.writeable = True
    image = cv2.cvtColor(image, cv2.COLOR_RGB2BGR)

    # If hands are detected, send selected landmarks via UDP
    if results.multi_hand_landmarks:
        for landmarks in results.multi_hand_landmarks:
            hand_data = {}

            # For example, sending positions of specific bones: wrist, index finger, thumb
            wrist = landmarks.landmark[mp_hands.HandLandmark.WRIST]
            index_tip = landmarks.landmark[mp_hands.HandLandmark.INDEX_FINGER_TIP]
            thumb_tip = landmarks.landmark[mp_hands.HandLandmark.THUMB_TIP]

            hand_data['wrist'] = {"x": wrist.x, "y": wrist.y, "z": wrist.z}
            hand_data['index_finger'] = {"x": index_tip.x, "y": index_tip.y, "z": index_tip.z}
            hand_data['thumb'] = {"x": thumb_tip.x, "y": thumb_tip.y, "z": thumb_tip.z}

            # Send hand data over UDP
            data = json.dumps(hand_data)
            sock.sendto(data.encode(), (UDP_IP, UDP_PORT))

            # Draw landmarks on the frame
            mp_drawing.draw_landmarks(image, landmarks, mp_hands.HAND_CONNECTIONS)

    # Display the image
    cv2.imshow("Hand Tracking", image)
    
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
