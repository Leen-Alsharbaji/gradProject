import socket
import cv2
import numpy as np
from ultralytics import YOLO
import struct
import json

# 1. Load the POSE model
model = YOLO("yolov8n-pose.pt")

HOST = '127.0.0.1'
PORT = 9999

def recvall(sock, size):
    data = b""
    while len(data) < size:
        packet = sock.recv(size - len(data))
        if not packet:
            return None
        data += packet
    return data

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.bind((HOST, PORT))
server.listen(1)
print("🚑 YOLO-Pose S&R server waiting for Unity connection...")

conn, addr = server.accept()
print("Connected by", addr)

try:
    while True:
        # Receive image length
        packed_msg_size = recvall(conn, 4)
        if not packed_msg_size:
            break
        msg_size = struct.unpack("!I", packed_msg_size)[0]

        # Receive image data
        frame_data = recvall(conn, msg_size)
        if not frame_data:
            break

        # Decode frame
        nparr = np.frombuffer(frame_data, np.uint8)
        frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        if frame is None:
            continue

        results = model(frame)[0]
        victims = []

        # Get inference time 
        inference_time = float(results.speed['inference']) if hasattr(results, 'speed') and 'inference' in results.speed else 0.0

        # 2. Extract Data
        if results.keypoints is not None and len(results.keypoints.data) > 0:
            for i, kpts in enumerate(results.keypoints.data):
                if len(kpts) == 0: continue
                
                kpts_np = kpts.cpu().numpy()
                
                # Get Torso Center (For Unity's 3D Raycast)
                shoulders_x = (kpts_np[5][0] + kpts_np[6][0]) / 2
                shoulders_y = (kpts_np[5][1] + kpts_np[6][1]) / 2
                hips_x = (kpts_np[11][0] + kpts_np[12][0]) / 2
                hips_y = (kpts_np[11][1] + kpts_np[12][1]) / 2
                
                center_x = (shoulders_x + hips_x) / 2
                center_y = (shoulders_y + hips_y) / 2

                # Confidence checks
                shoulder_conf = (kpts_np[5][2] + kpts_np[6][2]) / 2
                hip_conf = (kpts_np[11][2] + kpts_np[12][2]) / 2
                box_conf = float(results.boxes.conf[i]) if results.boxes is not None else 0.5

                # Extract Bounding Box [x1, y1, x2, y2]
                if results.boxes is not None and len(results.boxes.xyxy) > i:
                    box_coords = results.boxes.xyxy[i].cpu().numpy().astype(int).tolist()
                else:
                    box_coords = [0, 0, 0, 0]

                # 3. New S&R Logic: Aspect Ratio + Torso Angle
                box_width = box_coords[2] - box_coords[0]
                box_height = box_coords[3] - box_coords[1]
                
                aspect_ratio = box_width / box_height if box_height > 0 else 0
                status = "Standing / Sitting"

                is_horizontal = False

                # Metric A: Bounding Box Aspect Ratio
                if aspect_ratio > 1.2:
                    is_horizontal = True

                # Metric B: Torso Angle (if keypoints are confident enough)
                if shoulder_conf > 0.5 and hip_conf > 0.5:
                    dx = hips_x - shoulders_x
                    dy = hips_y - shoulders_y
                    angle = abs(np.degrees(np.arctan2(dy, dx)))
                    
                    # 90 degrees is upright. 0 degrees is laying head-left, 180 degrees is laying head-right.
                    if angle < 35 or angle > 145:
                        is_horizontal = True

                if is_horizontal:
                    status = "Horizontal / Unconscious"

                # 4. Only send the data if we are confident
                if box_conf > 0.6: 
                    victims.append({
                        "status": status,
                        "screenX": float(center_x),
                        "screenY": float(center_y),
                        "box": box_coords,  # <--- ADD THIS LINE BACK
                        "boxCenterX": float((box_coords[0] + box_coords[2]) / 2),
                        "boxCenterY": float((box_coords[1] + box_coords[3]) / 2),
                        "boxWidth": float(box_width),
                        "boxHeight": float(box_height),
                        "conf": box_conf,
                        "inferenceTime": inference_time
                    })
                    
        # Send custom JSON back to Unity
        response = json.dumps(victims).encode()
        conn.sendall(struct.pack("!I", len(response)) + response)

except Exception as e:
    print("Error:", e)
finally:
    conn.close()
    server.close()
    print("Server closed.")