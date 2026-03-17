# Gesture Recognition Car Building Game

A Unity-based therapeutic rehabilitation game that uses real-time hand gesture recognition to simulate car assembly tasks. Developed as part of ongoing research at the Computational Neuroinformatics Laboratory at NJIT.

## Overview

This project combines MediaPipe hand tracking with Unity game mechanics to create an interactive car-building experience. Players use hand gestures to pick up, rotate, and assemble car parts, while EEG data is simultaneously recorded for neuroscientific research.

## Features

- **Real-time Hand Tracking** — MediaPipe-powered gesture recognition for pinch, grab, and rotate mechanics
- **Car Assembly Gameplay** — Multi-level car building tasks with ghost placement targets and completion tracking
- **EEG/LSL Integration** — Synchronized EEG data collection via LSL and MMBT-S serial triggers for DSI-Flex
- **Research Data Logging** — High-frequency CSV data logging at 10ms intervals for research analysis
- **Rehabilitation Focus** — Designed for motor skill rehabilitation and cognitive engagement

## Tech Stack

- **Unity** (C#)
- **MediaPipe** — Hand tracking and gesture recognition
- **Python** — Backend gesture processing
- **LSL (Lab Streaming Layer)** — EEG synchronization
- **DSI-Flex EEG** — Brainwave data collection

## Getting Started

### Prerequisites

- Unity 2021.3 or later
- Python 3.8+
- MediaPipe (`pip install mediapipe`)
- LSL library

### Installation

1. Clone the repository
   ```bash
   git clone https://github.com/ZhongZhuolin/Gesture-Recognition-Car-Building-Game.git
   ```
2. Open the project in Unity
3. Unity will automatically regenerate Library, Temp, and project files
4. Run the MediaPipe Python server before launching the game

## Research Context

This game is part of the **SMART Platform** — a suite of VR/gesture-based therapeutic games developed for children with ASD and ADHD at NJIT's Computational Neuroinformatics Laboratory under Dr. Xiaobo Li.

## Author

**Walter Zhong (Zhuolin)**  
Electrical Engineering, NJIT  
Research Assistant — Computational Neuroinformatics Laboratory  
