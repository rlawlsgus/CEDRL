import pandas as pd
import numpy as np
import os
import glob
import json

# =================================DATASET
def read_trajectories(folder_path):
    file_paths = glob.glob(os.path.join(folder_path, "*.csv"))
    columns = ['timestep', 'pos_x', 'pos_z']
    trajectories = {}
    for file_path in file_paths:
        filename = os.path.basename(file_path)
        df = pd.read_csv(file_path, header=None, usecols=[0, 1, 2], names=columns, sep=';', skiprows=1)
        
        # Remove rows with the same consecutive timestep
        df = df.drop_duplicates(subset='timestep', keep='first')
        trajectories[filename] = df
    return trajectories

# =================================SPEED/DIRECTION SCORE
def calculate_speed_direction_scores(trajectories, window_size=2, max_speed=2):
    metrics = {}
    max_speed_diversity = 0
    max_direction_diversity = 0

    agents_vectors = {}
    for agent_id, trajectory in trajectories.items():
        agents_vectors[agent_id] = calculate_trajectory_vectors(trajectory, window_size)

    # First, calculate speed diversity and direction diversity for each agent
    for agent_id, vectors in agents_vectors.items():
        speeds = [np.clip(vector_magnitude(v) / window_size, 0, max_speed) for v in vectors]
        direction_changes = [vectors_dot(vectors[i], vectors[i+1]) for i in range(len(vectors)-1)]
        speed_diversity = np.std(speeds) if len(speeds) > 1 else 0
        direction_diversity = np.std(direction_changes) if direction_changes else 0

        metrics[agent_id] = {"speed_diversity": speed_diversity, "direction_diversity": direction_diversity}
        
        # Update the max values
        max_speed_diversity = max(max_speed_diversity, speed_diversity)
        max_direction_diversity = max(max_direction_diversity, direction_diversity)

    combined_metrics = {}
    for agent_id in metrics.keys():
        normalized_speed = metrics[agent_id]["speed_diversity"] / max_speed_diversity
        normalized_direction = metrics[agent_id]["direction_diversity"] / max_direction_diversity
        # Calculate the average of the normalized speed and direction diversity
        combined_value = np.clip(normalized_speed + normalized_direction, 0, 1)
        combined_metrics[agent_id] = combined_value

    return combined_metrics

def calculate_trajectory_vectors(trajectory, window_size):
    # Initialize the list to store the result vectors
    vectors = []
    
    # Calculate the number of rows for each window based on window size and timesteps
    timesteps = trajectory['timestep'].diff().median()  # Median timestep difference as approximate interval
    window_size_rows = max(1, int(window_size / timesteps))
    
    for window_start in range(0, len(trajectory), window_size_rows):
        window_end = min(window_start + window_size_rows, len(trajectory))
        window = trajectory.iloc[window_start:window_end]
        
        # Calculate the displacement vector for the window
        start_pos = window.iloc[0][['pos_x', 'pos_z']].values
        end_pos = window.iloc[-1][['pos_x', 'pos_z']].values
        displacement_vector = end_pos - start_pos
        
        vectors.append(displacement_vector)
    
    return vectors

def vector_magnitude(vector):
    """Calculate the magnitude of a 2D vector."""
    return np.linalg.norm(vector)

def vectors_dot(v1, v2):
    """Calculate the angle (in radians) between two vectors."""
    unit_v1 = v1 / np.linalg.norm(v1) if np.linalg.norm(v1) > 0 else v1
    unit_v2 = v2 / np.linalg.norm(v2) if np.linalg.norm(v2) > 0 else v2
    dot = (np.dot(unit_v1, unit_v2) + 1) / 2
    angle = np.clip(dot, 0, 1.0)
    return angle

# =================================GOAL DEVIATION SCORE
def calculate_goal_deviation_scores(trajectories):
    scores = {}
    for filename, traj in trajectories.items():
        scores[filename] = goal_deviation_score(traj)
    return scores

def goal_deviation_score(trajectory):
    # Calculate the straight-line distance from start to goal
    start = trajectory.iloc[0]
    goal = trajectory.iloc[-1]
    straight_line_distance = np.sqrt((goal['pos_x'] - start['pos_x'])**2 + (goal['pos_z'] - start['pos_z'])**2)

    if straight_line_distance == 0:
        return 0

    # Calculate the actual path length
    path_length = 0
    for i in range(1, len(trajectory)):
        segment = trajectory.iloc[i] - trajectory.iloc[i - 1]
        path_length += np.sqrt(segment['pos_x']**2 + segment['pos_z']**2)

    gamma = 2
    deviation_score = 1 - (1 / (1 + gamma * (path_length - straight_line_distance)))
    return np.clip(deviation_score, 0, 1)

# =================================GROUPING/MOVING TOGETHER SCORE
def calculate_grouping_scores(trajectories):
    scores = {}
    for filename, traj in trajectories.items():
        scores[filename] = grouping_score(traj, trajectories)
    return scores

def grouping_score(traj, trajectories, spawn_proximity_threshold=2, time_proximity_threshold=2):
    # Calculate moving together scores with other trajectories that have similar spawn points and times
    moving_together_scores = []
    for filename, other_traj in trajectories.items():
        if not other_traj.equals(traj):  # Ensure not comparing the trajectory with itself
            start_distance = calculate_start_distance(traj, other_traj)
            time_difference = calculate_time_difference(traj, other_traj)
            if start_distance <= spawn_proximity_threshold and time_difference <= time_proximity_threshold:
                moving_together_sc = moving_together_score(traj, other_traj)
                moving_together_scores.append(moving_together_sc)

    # Aggregate the moving together scores
    if moving_together_scores:
        moving_together_avg = np.mean(moving_together_scores)
    else:
        moving_together_avg = 0
    return moving_together_avg

def calculate_start_distance(traj1, traj2):
    start1 = traj1.iloc[0][['pos_x', 'pos_z']]
    start2 = traj2.iloc[0][['pos_x', 'pos_z']]
    return np.linalg.norm(start1 - start2)

def calculate_time_difference(traj1, traj2):
    start_time1 = traj1.iloc[0]['timestep']
    start_time2 = traj2.iloc[0]['timestep']
    return abs(start_time1 - start_time2)

def moving_together_score(traj1, traj2, proximity_threshold=3.6):
    # Ensure both trajectories are of the same length
    min_length = min(len(traj1), len(traj2))
    traj1 = traj1.iloc[:min_length]
    traj2 = traj2.iloc[:min_length]

    # Calculate pairwise distances at each timestep
    distances = np.linalg.norm(traj1[['pos_x', 'pos_z']].values - traj2[['pos_x', 'pos_z']].values, axis=1)

    # Check if agents are within proximity threshold at each timestep
    close_proximity = distances < proximity_threshold

    # Calculate the proportion of time spent in close proximity
    proportion_in_proximity = np.sum(close_proximity) / len(distances)

    average_distance = np.mean(distances)
    # Normalize and invert the distance to get the score
    distance_score = 1 - (average_distance / proximity_threshold)
    score = distance_score * proportion_in_proximity

    return np.clip(score, 0, 1)

# =================================RUN
def calculate_complexities(trajectories):
    speed_direction_scores = calculate_speed_direction_scores(trajectories)
    goal_deviation_scores = calculate_goal_deviation_scores(trajectories)
    grouping_scores = calculate_grouping_scores(trajectories)

    scores = {}
    for key in speed_direction_scores:
        # Apply 50% weight to grouping_score, and distribute the remaining 50% equally between the other two scores
        weighted_score = 0.25 * speed_direction_scores[key] + 0.25 * goal_deviation_scores[key] + 0.5 * grouping_scores[key]
        scores[key] = {"score": weighted_score, "speed_dir": speed_direction_scores[key], "goal_dev": goal_deviation_scores[key], "gr": grouping_scores[key]}
    return scores

def normalize_scores_dict(scores_dict, max_score=0.88):
    normalized_dict = {}
    
    for key, value in scores_dict.items():
        normalized_score = value['score'] / max_score
        normalized_score = min(normalized_score, 1)
        normalized_dict[key] = value.copy()
        normalized_dict[key]['norm_score'] = normalized_score
    
    return normalized_dict

def main():
    dataset = "dataset-name"
    path = f".PATH-TO-DATASET/{dataset}"
    out_path = ".OUT-PATH/scores.json"

    # Read trajectories from the specified path
    trajectories = read_trajectories(path)
    # Claculate complexities from given trajectories
    scores = calculate_complexities(trajectories)
    # Normalize the scores
    scores = normalize_scores_dict(scores)

    # Save the scores to the output path
    out_dir = os.path.dirname(out_path)
    os.makedirs(out_dir, exist_ok=True)
    with open(out_path, 'w') as file:
        json.dump(scores, file)
        
if __name__ == "__main__":
    main()

