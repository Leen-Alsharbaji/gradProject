from fastapi import FastAPI, UploadFile, File
from vosk import Model, KaldiRecognizer
import wave
import io
import json
import uvicorn

app = FastAPI()

# 1. LOAD THE MODEL DIRECTLY
print("⏳ Loading Vosk Model... this might take a few seconds...")
try:
    # This tells Vosk to look for the folder named "model" sitting right next to this script
    model = Model("C:\\Users\\lujin\\OneDrive\\Desktop\\S&R robot test 2\\PythonBackend\\audioCode\\model")
    print("✅ Vosk Model loaded successfully!")
except Exception as e:
    print("❌ FAILED to load Vosk model. Make sure the 'model' folder is exactly next to this script!")
    exit()

@app.post("/analyze-audio")
async def analyze_audio(file: UploadFile = File(...)):
    print("📥 Received audio file from Unity...")
    
    try:
        # Read the raw audio bytes Unity sent us
        audio_bytes = await file.read()
        
        # Open the bytes as a WAV file
        wf = wave.open(io.BytesIO(audio_bytes), "rb")
        
        # 2. INITIALIZE THE AI
        # We tell Vosk what sample rate Unity is using
        rec = KaldiRecognizer(model, wf.getframerate())
        
        # 3. PROCESS THE AUDIO
        # Feed the audio frames into the AI
        rec.AcceptWaveform(wf.readframes(wf.getnframes()))
        
        # Extract the JSON result
        result_json = rec.Result()
        result_dict = json.loads(result_json)
        text = result_dict.get("text", "").lower()
        
        print(f"🤖 LOCAL AI heard: '{text}'")
        
        # 4. CHECK FOR THE TRIGGER WORD
        if "help" in text:
            print("🚨 TRIGGER WORD DETECTED!")
            return {"status": "success", "trigger_detected": True, "text": text}
        else:
            return {"status": "success", "trigger_detected": False, "text": text}
            
    except Exception as e:
        print(f"❌ Error: {e}")
        return {"status": "error", "trigger_detected": False, "message": str(e)}

if __name__ == "__main__":
    print("🚀 OFFLINE AI Audio Server starting on port 8000...")
    uvicorn.run(app, host="127.0.0.1", port=8000)