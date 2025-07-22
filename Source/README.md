## 🔧 Setup & Training

### 1. Environment Setup
- Create a Python virtual environment using version **3.7.9**:
  ```bash
  python3.7 -m venv venv
  source venv/bin/activate  # On Windows: venv\Scripts\activate
  ```

- Install required packages:
  ```bash
  pip install -r requirements.txt
  ```

### 2. Unity Project
- Open the Unity project in the `CEDRL-Unity` folder.
- Build the project including the scene:
  ```
  Scenes/Training
  ```

### 3. Dataset Complexity Scores
- Complexity scores are precomputed and stored in:
  ```
  CEDRL-Unity/Assets/StreamingAssets/Datasets
  ```
- To compute new scores, run:
  ```bash
  python complexity.py
  ```
  Refer to Section 4.1 of the accompanying paper for methodology.

### 4. Training
- Use the following command to start training:
  ```bash
  mlagents-learn config/config.yaml --num-envs=N --env=./CEDRL-Unity/Builds/CEDRL-Unity.exe --run-id=CEDRL_0
  ```
  Replace `N` with the number of environments you wish to run in parallel (depending on your available system memory).

### 5. Monitoring Training
- Launch TensorBoard to monitor training progress:
  ```bash
  tensorboard --logdir results --port 6006
  ```
- Open a browser and navigate to:
  ```
  http://localhost:6006/
  ```


## 🧠 Inference

### Available Scenes
Two Unity scenes are configured for inference:

#### 1. `Scenes/Inference_Dataset`
- Use this to run inference using the environment and complexity scores from a selected dataset.
- Set the dataset on the `Scene` GameObject via the **"Selected Dataset"** dropdown in the Unity Inspector.

#### 2. `Scenes/Inference/Infinite`
- Run inference in an infinite environment.
- On the `Scene` GameObject, configure:
  - Number of agents.
  - Dataset to sample complexity scores from.
  - (Optional) Enable **"Manual Complexity"** to override scores:
    - Check the **"Manual Complexity"** box.
    - Use the slider below to set a manual complexity value.

---

