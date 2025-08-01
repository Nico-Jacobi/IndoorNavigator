import json
import math
from dataclasses import dataclass, field
import matplotlib.pyplot as plt
import matplotlib.image as mpimg
from pathlib import Path
from typing import List, Tuple, Dict

import numpy as np


@dataclass
class Position:
    X: float
    Y: float
    Floor: int


@dataclass
class PathData:
    file_name: str
    positions: List[Position]


@dataclass
class DeviceData:
    device_name: str
    simple_paths: List[PathData] = field(default_factory=list)
    kalman_paths: List[PathData] = field(default_factory=list)


@dataclass
class SessionData:
    session_name: str
    devices: List[DeviceData] = field(default_factory=list)


def load_positions_from_file(json_file: Path) -> List[Position]:
    with json_file.open() as f:
        data = json.load(f)
        return [Position(X=point["X"], Y=point["Y"], Floor=point["Floor"]) for point in data]


def load_position_data(base_path: Path) -> List[SessionData]:
    sessions = []
    collected_data_path = base_path / "resources" / "CollectedData"

    for session_dir in collected_data_path.iterdir():
        if not session_dir.is_dir():
            continue

        session = SessionData(session_name=session_dir.name)

        for device_dir in session_dir.iterdir():
            if not device_dir.is_dir():
                continue

            device_data = DeviceData(device_name=device_dir.name)

            for json_file in device_dir.glob("*.json"):
                positions = load_positions_from_file(json_file)
                path_data = PathData(file_name=json_file.name, positions=positions)

                if "simple_positions" in json_file.name:
                    device_data.simple_paths.append(path_data)
                elif "kalman_positions" in json_file.name:
                    device_data.kalman_paths.append(path_data)

            session.devices.append(device_data)
        sessions.append(session)

    return sessions


def plot_paths(paths: List[PathData], ground_truth: PathData, title: str, plot_points: bool = False, plot_ground_truth: bool = True, plot_background: bool = True):
    plt.figure(figsize=(12, 12))

    if plot_background:
        img = mpimg.imread("resources/Ebene3.png")
        img_extent = (-50, 30, -18, 57)
        plt.imshow(img, extent=img_extent, origin='lower', alpha=0.8)

    if plot_ground_truth:
        xs_gt = [p.X for p in ground_truth.positions]
        ys_gt = [p.Y for p in ground_truth.positions]
        plt.plot(xs_gt, ys_gt, label="Ground Truth", color="red", linewidth=2, zorder=10)


    for path in paths:
        xs = [p.X for p in path.positions]
        ys = [p.Y for p in path.positions]
        plt.plot(xs, ys, linewidth=1, alpha=0.5)

    if plot_points:
        for path in paths:
            xs = [p.X for p in path.positions]
            ys = [p.Y for p in path.positions]
            plt.plot(xs, ys, linewidth=1, alpha=0.5)
            plt.scatter(xs, ys, s=5, alpha=0.5)

    plt.title(title)
    plt.xlabel("X")
    plt.ylabel("Y")
    plt.legend()  # will only show Ground Truth label
    plt.grid(True)
    plt.show()

def closest_distance_to_polyline(point: Position, polyline: List[Position]) -> float:
    """
    Compute the minimum distance from a point to a polyline defined by a sequence of Positions.
    """
    min_dist = float('inf')
    for i in range(len(polyline) - 1):
        a, b = polyline[i], polyline[i+1]
        # vector from a to b
        dx, dy = b.X - a.X, b.Y - a.Y
        if dx == 0 and dy == 0:
            # Degenerate segment
            dist = euclidean_distance(point, a)
        else:
            # Project point onto line segment
            t = ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / (dx*dx + dy*dy)
            t = max(0, min(1, t))  # Clamp to segment
            nearest_x = a.X + t * dx
            nearest_y = a.Y + t * dy
            dist = math.hypot(point.X - nearest_x, point.Y - nearest_y)
        min_dist = min(min_dist, dist)
    return min_dist

def euclidean_distance(p1: Position, p2: Position) -> float:
    """Calculate 2D Euclidean distance ignoring Floor differences."""
    return math.sqrt((p1.X - p2.X) ** 2 + (p1.Y - p2.Y) ** 2)



def calculate_stats_to_ground_truth(
    paths: List[PathData],
    ground_truth: PathData
) -> Dict[str, float]:
    """
    Calculate distance statistics of all path points to the ground truth polyline.

    Returns a dict with:
    - mean: average distance
    - median: median distance
    - std_dev: standard deviation of distances
    - min: minimum distance
    - max: maximum distance
    - p25: 25th percentile
    - p75: 75th percentile
    - count: number of points considered
    """
    distances = []
    polyline = ground_truth.positions
    for path in paths:
        for p in path.positions:
            dist = closest_distance_to_polyline(p, polyline)
            distances.append(dist)

    if not distances:
        return {k: float('nan') for k in ["mean", "median", "std_dev", "min", "max", "p25", "p75", "count"]}

    distances_np = np.array(distances)
    return {
        "mean": float(distances_np.mean()),
        "median": float(np.median(distances_np)),
        "std_dev": float(distances_np.std()),
        "min": float(distances_np.min()),
        "max": float(distances_np.max()),
        "p25": float(np.percentile(distances_np, 25)),
        "p75": float(np.percentile(distances_np, 75)),
        "p5": float(np.percentile(distances_np, 5)),
        "p95": float(np.percentile(distances_np, 95)),
        "p1": float(np.percentile(distances_np, 1)),
        "p99": float(np.percentile(distances_np, 99)),
        "count": len(distances_np),
    }


def format_stats_for_filename(stats: Dict[str, float]) -> str:
    """Create a compact filename-safe string from stats."""
    return (
        f"mean{stats['mean']:.2f}_"
        f"med{stats['median']:.2f}_"
        f"std{stats['std_dev']:.2f}_"
        f"max{stats['max']:.2f}_"
        f"p25{stats['p25']:.2f}_"
        f"p75{stats['p75']:.2f}_"
        f"p5{stats['p5']:.2f}_"
        f"p95{stats['p95']:.2f}_"
        f"p1{stats['p1']:.2f}_"
        f"p99{stats['p99']:.2f}"
    )


def plot_and_save_paths(
    paths: List[PathData],
    ground_truth: PathData,
    title: str,
    filename_base: str,
    std_dev: float,
    min_dev: float,
    max_dev: float,
    output_dir: Path,
    plot_points: bool = False,
    plot_ground_truth: bool = True,
    plot_background: bool = True
):
    """
    Plots paths, optionally overlays ground truth, points, background.
    Saves figure in the given output directory.
    """
    plt.figure(figsize=(12, 12))

    if plot_background:
        img = mpimg.imread("resources/Ebene3.png")
        img_extent = (-50, 30, -18, 57)
        plt.imshow(img, extent=img_extent, origin='lower', alpha=0.8)

    if plot_ground_truth:
        xs_gt = [p.X for p in ground_truth.positions]
        ys_gt = [p.Y for p in ground_truth.positions]
        plt.plot(xs_gt, ys_gt, label="Ground Truth", color="red", linewidth=2, zorder=10)

    for path in paths:
        xs = [p.X for p in path.positions]
        ys = [p.Y for p in path.positions]
        plt.plot(xs, ys, linewidth=1, alpha=0.5)
        if plot_points:
            plt.scatter(xs, ys, s=5, alpha=0.5)

    plt.title(f"{title}")
    plt.xlabel("X")
    plt.ylabel("Y")
    plt.legend()
    plt.grid(True)

    output_dir.mkdir(parents=True, exist_ok=True)
    filename = f"{filename_base}_std{std_dev:.2f}_min{min_dev:.2f}_max{max_dev:.2f}.png"
    filename = filename.replace(' ', '_').replace(':', '')
    save_path = output_dir / filename

    plt.savefig(save_path, dpi=300)
    print(f"Saved plot as {save_path}")
    plt.close()



def save_all(sessions, real_path: PathData, plot_points=False, plot_ground_truth=True, plot_background=True):
    output_dir = Path(__file__).parent / "resources" / "Graphics"
    output_dir.mkdir(parents=True, exist_ok=True)

    for session in sessions:
        for device in session.devices:
            if device.simple_paths:
                stats = calculate_stats_to_ground_truth(device.simple_paths, real_path)
                stats_str = format_stats_for_filename(stats)
                plot_and_save_paths(
                    device.simple_paths, real_path,
                    title=f"{session.session_name} - {device.device_name} - Simple\nØ={stats['mean']:.2f}m, σ={stats['std_dev']:.2f}m, max={stats['max']:.2f}m",
                    filename_base=f"{session.session_name}_{device.device_name}_simple_{stats_str}",
                    std_dev=stats["std_dev"], min_dev=0, max_dev=stats["max"],
                    output_dir=output_dir,
                    plot_points=plot_points,
                    plot_ground_truth=plot_ground_truth,
                    plot_background=plot_background
                )
            if device.kalman_paths:
                stats = calculate_stats_to_ground_truth(device.kalman_paths, real_path)
                stats_str = format_stats_for_filename(stats)
                plot_and_save_paths(
                    device.kalman_paths, real_path,
                    title=f"{session.session_name} - {device.device_name} - Kalman\nØ={stats['mean']:.2f}m, σ={stats['std_dev']:.2f}m, max={stats['max']:.2f}m",
                    filename_base=f"{session.session_name}_{device.device_name}_kalman_{stats_str}",
                    std_dev=stats["std_dev"], min_dev=0, max_dev=stats["max"],
                    output_dir=output_dir,
                    plot_points=plot_points,
                    plot_ground_truth=plot_ground_truth,
                    plot_background=plot_background
                )


def save_individual_paths(sessions, real_path: PathData, plot_points=False, plot_ground_truth=True, plot_background=True):
    output_dir = Path(__file__).parent / "resources" / "Graphics" / "Individual"
    output_dir.mkdir(parents=True, exist_ok=True)

    for session in sessions:
        for device in session.devices:
            for idx, path in enumerate(device.simple_paths or []):
                stats = calculate_stats_to_ground_truth([path], real_path)
                stats_str = format_stats_for_filename(stats)
                plot_and_save_paths(
                    [path], real_path,
                    title=f"{session.session_name} - {device.device_name} - Simple #{idx}\nØ={stats['mean']:.2f}m, σ={stats['std_dev']:.2f}m, max={stats['max']:.2f}m",
                    filename_base=f"{session.session_name}_{device.device_name}_simple_{idx}_{stats_str}",
                    std_dev=stats["std_dev"], min_dev=0, max_dev=stats["max"],
                    output_dir=output_dir,
                    plot_points=plot_points,
                    plot_ground_truth=plot_ground_truth,
                    plot_background=plot_background
                )

            for idx, path in enumerate(device.kalman_paths or []):
                stats = calculate_stats_to_ground_truth([path], real_path)
                stats_str = format_stats_for_filename(stats)
                plot_and_save_paths(
                    [path], real_path,
                    title=f"{session.session_name} - {device.device_name} - Kalman #{idx}\nØ={stats['mean']:.2f}m, σ={stats['std_dev']:.2f}m, max={stats['max']:.2f}m",
                    filename_base=f"{session.session_name}_{device.device_name}_kalman_{idx}_{stats_str}",
                    std_dev=stats["std_dev"], min_dev=0, max_dev=stats["max"],
                    output_dir=output_dir,
                    plot_points=plot_points,
                    plot_ground_truth=plot_ground_truth,
                    plot_background=plot_background
                )


def main():
    manual_positions = [
        Position(X=-33, Y=-5.5, Floor=3),
        Position(X=-7, Y=-5.5, Floor=3),
        Position(X=-3, Y=-8, Floor=3),
        Position(X=0, Y=-9, Floor=3),
        Position(X=3, Y=-8, Floor=3),
        Position(X=5, Y=-5, Floor=3),
        Position(X=8, Y=2, Floor=3),
        Position(X=9.5, Y=5, Floor=3),

        Position(X=9.5, Y=40.5, Floor=3),
        Position(X=8, Y=42, Floor=3),

        Position(X=-18, Y=42, Floor=3),

        Position(X=-20.5, Y=44.5, Floor=3),
        Position(X=-23.5, Y=45.5, Floor=3),

        Position(X=-36, Y=45.5, Floor=3),
        Position(X=-37, Y=44.5, Floor=3),

        Position(X=-37, Y=27, Floor=3),

    ]

    real_path = PathData(
        file_name="none",
        positions=manual_positions
    )


    base_path = Path(__file__).parent
    sessions = load_position_data(base_path)

    # Simple console summary
    for session in sessions:
        print(f"Session: {session.session_name}")
        for device in session.devices:
            print(f"  Device: {device.device_name}")
            print(f"    Simple paths: {len(device.simple_paths)}")
            print(f"    Kalman paths: {len(device.kalman_paths)}")

    # Plot example: first session, first device, simple paths
    if sessions:
        first_session = sessions[3]
        if first_session.devices:
            first_device = first_session.devices[0]

            for session in sessions:
                print(f"\nSession: {session.session_name}")

            print(f"\nPlotting simple paths for {first_device.device_name} in session {first_session.session_name}")

            stats_simple = calculate_stats_to_ground_truth(first_device.simple_paths, real_path)
            print(f"Simple paths deviation:")
            print(f"  mean = {stats_simple['mean']:.2f} m")
            print(f"  std_dev = {stats_simple['std_dev']:.2f} m")
            print(f"  median = {stats_simple['median']:.2f} m")
            print(f"  max = {stats_simple['max']:.2f} m")
            print(f"  p25 = {stats_simple['p25']:.2f} m")
            print(f"  p75 = {stats_simple['p75']:.2f} m")

            plot_paths(
                first_device.simple_paths,
                real_path,
                f"{first_device.device_name} - Simple Paths\nØ={stats_simple['mean']:.2f} m, σ={stats_simple['std_dev']:.2f} m, max={stats_simple['max']:.2f} m"
            )

            stats_kalman = calculate_stats_to_ground_truth(first_device.kalman_paths, real_path)
            print(f"\nKalman paths deviation:")
            print(f"  mean = {stats_kalman['mean']:.2f} m")
            print(f"  std_dev = {stats_kalman['std_dev']:.2f} m")
            print(f"  median = {stats_kalman['median']:.2f} m")
            print(f"  max = {stats_kalman['max']:.2f} m")
            print(f"  p25 = {stats_kalman['p25']:.2f} m")
            print(f"  p75 = {stats_kalman['p75']:.2f} m")

            #plot_paths(
            #    first_device.kalman_paths,
            #    real_path,
            #    f"{first_device.device_name} - Kalman Paths\nØ={stats_kalman['mean']:.2f} m, σ={stats_kalman['std_dev']:.2f} m, max={stats_kalman['max']:.2f} m"
            #)

    save_all(sessions, real_path, plot_points=False, plot_ground_truth=True, plot_background=True)
    save_individual_paths(sessions, real_path, plot_points=True, plot_ground_truth=False, plot_background=True)

if __name__ == "__main__":
    main()


# run this script to generate the plots and statistics for all sessions
# deviation only measured against the ground truth path -> difference in movement direction not considered