# Reaction Trainer

<img width="986" height="793" alt="image" src="https://github.com/user-attachments/assets/e62e6924-45eb-4382-8d44-9cf1e7e43af7" />

## Features

Technical Specifications
Dynamic Scaling & Resource Management
Implements an object-pooling logic where additional target instances instantiate every 20 successful hits. Reaching the 3-miss threshold triggers a list-clearing function that resets the environment to a single base object.

AES-256 Data Persistence
Serializes session data including best reaction time, total points, and record hits using 256-bit AES encryption. Data is stored in a local .dat file within the user's AppData directory to ensure secure, non-volatile progress tracking.

Velocity & Difficulty Calculus
Employs a linear scaling function to calculate target speed based on current round hits. Velocity is capped at 6.5f to maintain a balance between human reaction limits and increasing unpredictability.

GDI+ Environment Rendering
Utilizes a coordinate-based storage system to render persistent visual markers on the background. By decoupling rendering from collision detection, the system maintains high-precision hitboxes without graphical performance degradation.

Threading & Loop Architecture
Operates on a 10ms timer-driven loop utilizing OptimizedDoubleBuffer to mitigate screen tearing. Includes a real-time FPS counter calculated via Stopwatch frequency to monitor hardware-accelerated rendering performance.

State Logic & Scoring
Applies a +2/-10 point integer system with a boolean-driven pause state. This mechanism suspends all logic and timing calculations during menu interactions to prevent session time-leakage.

## Installation

1. Download .NET SDK: https://dotnet.microsoft.com/en-us/download
2. git clone https://github.com/q1lra/reaction-trainer
3. cd path/to/reaction-trainer-master
4. dotnet run
