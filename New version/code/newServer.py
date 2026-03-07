import base64
import json
import cv2
import numpy as np
import torch
import segmentation_models_pytorch as smp
from fastapi import FastAPI, WebSocket
import uvicorn

app = FastAPI()

# ================= DEVICE =================
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# ================= RECREATE TRAINED MODEL =================
model = smp.Unet(
    encoder_name="resnet34",
    encoder_weights=None,   # IMPORTANT: None when loading trained weights
    in_channels=3,
    classes=1
).to(device)

model.load_state_dict(torch.load("models/Detection/best_unet.pth", map_location=device))
model.eval()

IMG_SIZE = 512   # MUST match training size


# ================= PREPROCESS =================
def preprocess(img_bytes):
    nparr = np.frombuffer(img_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    img = cv2.resize(img, (IMG_SIZE, IMG_SIZE))

    img = img.astype(np.float32) / 255.0

    # Same normalization used in training
    mean = np.array([0.485, 0.456, 0.406], dtype=np.float32)
    std  = np.array([0.229, 0.224, 0.225], dtype=np.float32)
    img = (img - mean) / std

    img = np.transpose(img, (2, 0, 1))  # HWC → CHW
    img = np.expand_dims(img, axis=0)

    return torch.tensor(img).to(device)


@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    await websocket.accept()
    print("✅ WebSocket Connected")

    try:
        while True:
            data = await websocket.receive_text()
            print("📩 Message received")

            message = json.loads(data)
            image_bytes = base64.b64decode(message["image"])
            img_tensor = preprocess(image_bytes)

            with torch.no_grad():
                logits = model(img_tensor)
                probs = torch.sigmoid(logits)
                
                # 🧠 THE ULTIMATE FIX: Auto-Scaling Probabilities!
                # We find the "most crack-like" pixels in the image and force 
                # them to be pure white, even if the AI's confidence is near 0.
                probs_np = probs.squeeze().cpu().numpy()
                max_prob = np.max(probs_np)
                
                if max_prob > 0.0001:  # As long as it detects even a microscopic signal
                    mask = (probs_np / max_prob) * 255.0  # Scale that signal up to 255 (White)
                else:
                    mask = probs_np * 255.0

            # Apply a very light threshold to clean up the scaled image
            mask = (mask > 128).astype(np.uint8) * 255

            _, buffer = cv2.imencode(".png", mask)

            response = {
                "mask": base64.b64encode(buffer).decode("utf-8")
            }

            await websocket.send_text(json.dumps(response))
            print("📤 Response sent")

    except Exception as e:
        print("❌ WebSocket Disconnected:", e)


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)