# 🚗 Car Assembly Game

**A Unity-based hand-tracking rehabilitation game for children with ADHD and autism**

[![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black?logo=unity)](https://unity.com/)
[![MediaPipe](https://img.shields.io/badge/MediaPipe-Hands-blue)](https://mediapipe.dev/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 📖 Overview

The Car Assembly Game is a therapeutic video game designed to improve **fine motor skills** and **hand-eye coordination** in pediatric populations. Instead of traditional controllers, players use their **actual hand** in front of a webcam to grab, move, and place virtual car parts.

### ✨ Key Features

- **🖐️ Markerless Hand Tracking** - MediaPipe Hands integration (21 landmarks)
- **📈 Progressive Difficulty** - 5 levels with increasing challenges
- **🔄 Hand Rotation Control** - Level 5 introduces wrist rotation mechanics
- **📊 Research-Grade Data** - 100Hz sampling exported to CSV
- **🧠 EEG/LSL Ready** - Architecture supports neurophysiological synchronization
- **⚙️ Highly Configurable** - Extensive inspector settings for research protocols

---

## 🎮 How It Works

| Action | Gesture |
|--------|---------|
| Move cursor | Move hand in camera view |
| Pick up part | Pinch thumb + index finger |
| Drag part | Keep pinching while moving |
| Drop part | Release pinch |
| Rotate part | Tilt hand (Level 5 only) |

### Visual Feedback

| Color | Meaning |
|-------|---------|
| 🟢 **Green** | Position & rotation correct - release to snap! |
| 🟡 **Yellow** | Not close enough to target |
| 🟠 **Orange** | Position correct, rotation wrong (Level 5) |

---

## 📊 Level Progression

| Level | Challenge | Skills Developed |
|-------|-----------|------------------|
| **1** | Static Parts | Basic hand-eye coordination |
| **2** | Moving Parts | Visual tracking |
| **3** | Faster Movement | Reaction time |
| **4** | Ghost Bouncing | Spatial awareness |
| **5** | **Hand Rotation** | Bilateral coordination |

---

## 🛠️ Installation

### Prerequisites

- Unity 2022.3 LTS or newer
- Webcam (720p minimum, 1080p recommended)
- Windows 10/11 or macOS

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/YOUR_USERNAME/car-assembly-game.git
   cd car-assembly-game
   ```

2. **Open in Unity**
   - Open Unity Hub
   - Click "Add" → Browse to cloned folder
   - Open project with Unity 2022.3+

3. **Import MediaPipe**
   - The project includes MediaPipe Unity Plugin
   - If missing, import from [MediaPipe Unity Plugin](https://github.com/homuler/MediaPipeUnityPlugin)

4. **Run the game**
   - Open `Scenes/MainScene`
   - Press Play ▶️
   - Allow webcam access when prompted

---

## 📁 Project Structure

```
car-assembly-game/
├── Assets/
│   ├── Scripts/
│   │   ├── CarPartMover.cs          # Hand tracking & input
│   │   ├── CarAssemblyManager.cs    # Assembly logic & validation
│   │   ├── GameDataTracker.cs       # Research data collection
│   │   ├── LevelManager.cs          # Level progression
│   │   ├── LevelData.cs             # Level configuration (ScriptableObject)
│   │   ├── MovingCarPart.cs         # Part movement physics
│   │   ├── CompletionScreen.cs      # Level complete UI
│   │   ├── HandZone.cs              # Pickup zone enforcement
│   │   ├── AssemblyTrackingIntegrator.cs  # Data bridge
│   │   └── LockZPosition.cs         # 2D constraint
│   ├── Scenes/
│   ├── Prefabs/
│   └── ScriptableObjects/
│       └── Levels/                  # LevelData assets
├── GameData/                        # Generated data output
│   └── Session_YYYY-MM-DD_HH-MM-SS/
│       ├── session_info.txt
│       └── Level_X/
│           ├── Level_X_Data.csv
│           └── Level_X_Summary.txt
├── Documentation/
└── README.md
```

---

## ⚙️ Configuration

### Hand Tracking Settings (CarPartMover)

| Setting | Default | Description |
|---------|---------|-------------|
| `pinchThreshold` | 0.08 | Distance to trigger grab |
| `releaseThreshold` | 0.12 | Distance to release |
| `pickupRange` | 0.8 | Radius for part pickup |
| `smoothingFactor` | 0.5 | Position smoothing (0-1) |

### Rotation Settings (Level 5)

| Setting | Default | Description |
|---------|---------|-------------|
| `enableHandRotation` | false | Enable rotation control |
| `rotationMultiplier` | 1.0 | Part rotation speed vs hand |
| `snapAngleIncrement` | 45° | Snap positions (90°=4, 45°=8) |
| `snapTolerance` | 15° | Auto-snap threshold |
| `handAngleCalibration` | 0° | Offset for neutral hand |

### Data Collection Settings (GameDataTracker)

| Setting | Default | Description |
|---------|---------|-------------|
| `trackingIntervalMs` | 10 | Sample rate (10ms = 100Hz) |
| `saveFolderName` | GameData | Output directory |

---

## 📈 Research Data

### CSV Output (100Hz)

```csv
Time_ms,Palm_X,Palm_Y,Velocity_px_s,Acceleration_px_s2,Is_Dragging,Hand_Angle,Part_Rotation,Rotation_Error,...
0,512.3,384.2,0,0,0,0,0,0,...
10,514.1,385.0,180.5,18050,0,2.3,0,0,...
20,518.7,387.1,492.3,31180,1,5.1,5.1,84.9,...
```

### Key Columns

| Column | Description |
|--------|-------------|
| `Time_ms` | Milliseconds since level start |
| `Palm_X, Palm_Y` | Hand position (pixels) |
| `Velocity_px_s` | Hand speed |
| `Acceleration_px_s2` | Hand acceleration |
| `Is_Dragging` | 1 = holding part |
| `Hand_Angle` | Hand tilt (-180° to 180°) |
| `Part_Rotation` | Part Z rotation |
| `Rotation_Error` | |Part - Target| |
| `Placement_Success` | 1 = successful placement |

### Analysis Tools

```python
# Python example
import pandas as pd
data = pd.read_csv('GameData/Session_.../Level_5/Level_5_Data.csv')
avg_velocity = data['Velocity_px_s'].mean()
success_rate = data['Placement_Success'].sum() / data['Placement_Attempt'].sum()
```

---

## 🔧 Extending the Game

### Adding New Levels

1. **Create LevelData asset**
   - Right-click in Project → Create → Car Assembly → Level Data

2. **Configure parameters**
   - Set snap distance, movement speed, rotation settings

3. **Add to LevelManager**
   - Drag new LevelData to `levels` array

### Adding Custom Metrics

1. Add field to `TrackingDataPoint` struct in `GameDataTracker.cs`
2. Update `GetCsvHeader()` with column name
3. Update `TrackDataPoint()` to populate value
4. Update `ToCsvRow()` to export value

---

## 🧠 EEG/LSL Integration

The architecture supports Lab Streaming Layer for synchronized neurophysiological recording:

- **Event Markers**: Level start, part pickup, placement attempt, level complete
- **Continuous Streams**: Hand position, velocity, rotation angle
- **Timing**: High-resolution system clock timestamps

---

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open Pull Request

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 📬 Contact

lin.walterzhong@gmail.com

---

## 🙏 Acknowledgments

- [MediaPipe](https://mediapipe.dev/) for hand tracking
- [Unity](https://unity.com/) game engine
- Research supported by New Jersey Institute Of Technology Biomedical Department

---

<p align="center">
  <b>Built for pediatric motor rehabilitation research</b><br>
  <i>Improving lives through engaging therapeutic games</i>
</p>
