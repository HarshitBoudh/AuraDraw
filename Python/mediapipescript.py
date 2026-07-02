import cv2
import mediapipe as mp
import socket
import struct
import numpy as np

# ==============================
# UDP SETTINGS
# ==============================
UDP_IP = "127.0.0.1"
UDP_PORT = 5005
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

# ==============================
# MEDIAPIPE SETUP
# ==============================
mp_hands = mp.solutions.hands
mp_draw = mp.solutions.drawing_utils
hands = mp_hands.Hands(
    static_image_mode=False,
    max_num_hands=1,
    min_detection_confidence=0.75,
    min_tracking_confidence=0.75
)

# ==============================
# CAMERA — set to your actual resolution
# ==============================
cap = cv2.VideoCapture(0)
cap.set(cv2.CAP_PROP_FRAME_WIDTH, 1280)
cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 720)
cap.set(cv2.CAP_PROP_FPS, 60)           # Request 60fps for low latency

FRAME_W = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
FRAME_H = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
print(f"📷 Camera: {FRAME_W}x{FRAME_H}")

# ==============================
# WALL PROJECTION SMOOTHING
# ==============================
# Exponential moving average — higher alpha = more responsive, more jitter
# For wall: lower alpha (0.4–0.5) to reduce hand tremor amplification
POSITION_ALPHA = 0.45
PRESSURE_ALPHA = 0.20   # Pressure changes slowly and smoothly

smooth_x, smooth_y = 0.5, 0.5
smooth_pressure = 1.0
initialized = False

# ==============================
# DEPTH SETTINGS
# Calibrate: run script, note Z values at your near/far hand positions
# ==============================
DEPTH_CLOSE  = -0.12   # Z when hand is very close to camera
DEPTH_FAR    =  0.12   # Z when hand is arm's-length from camera
PRESSURE_MIN =  0.5    # Minimum brush multiplier (far)
PRESSURE_MAX =  3.5    # Maximum brush multiplier (close) — bigger for wall

# ==============================
# GESTURE SETTINGS
# Two-fingers-up threshold — increase if false triggering on wall
# ==============================
FINGER_UP_THRESHOLD = 0.035   # How much above knuckle tip must be

# ==============================
# NO-HAND TIMEOUT
# Send zero packet so Unity knows hand is gone
# ==============================
frames_without_hand = 0
NO_HAND_TIMEOUT_FRAMES = 6     # ~100ms at 60fps before sending "lost" signal

print("🎨 AuraDraw Wall Mode Started")
print(f"   DEPTH_CLOSE={DEPTH_CLOSE}  DEPTH_FAR={DEPTH_FAR}")
print("   Move hand CLOSER = BIGGER brush | FARTHER = SMALLER brush")
print("   Raise index + middle finger = toggle ERASER")
print("   Press ESC to quit")

# ==============================
# MAIN LOOP
# ==============================
while True:
    success, frame = cap.read()
    if not success:
        continue

    frame = cv2.flip(frame, 1)
    rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
    rgb.flags.writeable = False           # Small perf boost
    results = hands.process(rgb)
    rgb.flags.writeable = True

    if results.multi_hand_landmarks:
        frames_without_hand = 0
        hand_landmarks = results.multi_hand_landmarks[0]
        lm = hand_landmarks.landmark

        # Draw skeleton
        mp_draw.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)

        # ── Finger positions ──────────────────────────────────────
        index_tip  = lm[8]
        index_pip  = lm[6]    # Knuckle below tip
        middle_tip = lm[12]
        middle_pip = lm[10]
        ring_tip   = lm[16]
        pinky_tip  = lm[20]

        x = index_tip.x
        y = index_tip.y

        # ── Gesture: Two fingers up = Eraser ─────────────────────
        # Also check ring+pinky are down to avoid false triggers
        index_up  = index_tip.y  < index_pip.y  - FINGER_UP_THRESHOLD
        middle_up = middle_tip.y < middle_pip.y - FINGER_UP_THRESHOLD
        ring_down = ring_tip.y   > lm[14].y
        pinky_down= pinky_tip.y  > lm[18].y
        two_fingers_up = index_up and middle_up and ring_down and pinky_down

        # ── Depth → Pressure ─────────────────────────────────────
        raw_z     = index_tip.z
        clamped_z = max(DEPTH_CLOSE, min(DEPTH_FAR, raw_z))
        t         = (clamped_z - DEPTH_FAR) / (DEPTH_CLOSE - DEPTH_FAR)
        t         = max(0.0, min(1.0, t))
        raw_pressure = PRESSURE_MIN + t * (PRESSURE_MAX - PRESSURE_MIN)

        # ── Smooth position ───────────────────────────────────────
        if not initialized:
            smooth_x, smooth_y = x, y
            smooth_pressure    = raw_pressure
            initialized        = True
        else:
            smooth_x        = POSITION_ALPHA * x + (1 - POSITION_ALPHA) * smooth_x
            smooth_y        = POSITION_ALPHA * y + (1 - POSITION_ALPHA) * smooth_y
            smooth_pressure = PRESSURE_ALPHA * raw_pressure + (1 - PRESSURE_ALPHA) * smooth_pressure

        # ── Send to Unity ─────────────────────────────────────────
        message = struct.pack('ffff',
            smooth_x,
            smooth_y,
            1.0 if two_fingers_up else 0.0,
            smooth_pressure
        )
        sock.sendto(message, (UDP_IP, UDP_PORT))

        # ── Debug overlay ─────────────────────────────────────────
        status = "ERASER" if two_fingers_up else "DRAW"
        color  = (0, 60, 255) if two_fingers_up else (0, 220, 60)

        # Depth bar
        bar_fill = int(t * 300)
        cv2.rectangle(frame, (20, 160), (320, 185), (40, 40, 40), -1)
        cv2.rectangle(frame, (20, 160), (20 + bar_fill, 185), (0, 200, 255), -1)
        cv2.putText(frame, "FAR", (20, 200),  cv2.FONT_HERSHEY_SIMPLEX, 0.5, (150,150,150), 1)
        cv2.putText(frame, "CLOSE", (270, 200), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (150,150,150), 1)

        cv2.putText(frame, f"Mode: {status}",              (20, 50),  cv2.FONT_HERSHEY_SIMPLEX, 1.2, color, 3)
        cv2.putText(frame, f"Brush: {smooth_pressure:.2f}x", (20, 95),  cv2.FONT_HERSHEY_SIMPLEX, 1.0, (255,230,0), 2)
        cv2.putText(frame, f"Z={raw_z:.3f}  t={t:.2f}",   (20, 135), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (180,180,180), 2)

        # Finger dot on preview
        fx = int(smooth_x * FRAME_W)
        fy = int(smooth_y * FRAME_H)
        cv2.circle(frame, (fx, fy), 10, (255, 255, 255), -1)
        cv2.circle(frame, (fx, fy),  8, color, -1)

    else:
        # No hand detected
        frames_without_hand += 1
        initialized = False

        if frames_without_hand >= NO_HAND_TIMEOUT_FRAMES:
            # Send zero packet — Unity uses this to stop drawing
            message = struct.pack('ffff', 0.0, 0.0, 0.0, 1.0)
            sock.sendto(message, (UDP_IP, UDP_PORT))

        cv2.putText(frame, "No hand detected", (20, 50),
                    cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 80, 255), 2)

    cv2.imshow("AuraDraw - Wall Mode", frame)
    if cv2.waitKey(1) == 27:
        break

cap.release()
cv2.destroyAllWindows()
sock.close()
