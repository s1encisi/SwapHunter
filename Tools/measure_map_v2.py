"""从Unity NavMesh原始三角形复算十二章可达投影面积，不运行Unity、不修改原始输入。

依赖Shapely。旧分母取旧版全NavMesh的XZ并集；新版步行与经过实际验证的锚点闭包分别计量。
"""
from __future__ import annotations

import argparse
import json
import math
import sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
from pathlib import Path

try:
    from shapely.geometry import Polygon
    from shapely.ops import unary_union
except ImportError as error:
    raise SystemExit("需要Shapely：python -m pip install shapely") from error


def load_triangles(path: Path, chapter: int):
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if data.get("chapter") != chapter:
        raise ValueError(f"章节编号不一致：{path}")
    vertices, indices = data["vertices"], data["indices"]
    if not vertices or not indices or len(indices) % 3:
        raise ValueError(f"缺少完整三角形数据：{path}")
    points = [(float(vertex["x"]), float(vertex["z"])) for vertex in vertices]
    if any(not math.isfinite(value) for point in points for value in point):
        raise ValueError(f"顶点含非有限坐标：{path}")
    if any(type(index) is not int or not 0 <= index < len(points) for index in indices):
        raise ValueError(f"三角形索引越界：{path}")
    triangles = []
    for offset in range(0, len(indices), 3):
        polygon = Polygon([points[index] for index in indices[offset:offset + 3]])
        # Vertical/degenerate faces have no horizontal area, but retain their index.
        triangles.append(polygon if polygon.area > 1e-7 else Polygon())
    return data, triangles


def selected_area(triangles, data, field: str, path: Path):
    selection = data.get(field)
    if not isinstance(selection, list):
        raise ValueError(f"缺少实际可达集合 {field}：{path}；不会回退成全部NavMesh")
    if any(type(index) is not int or not 0 <= index < len(triangles) for index in selection):
        raise ValueError(f"可达三角形索引越界：{path} / {field}")
    return unary_union([triangles[index] for index in selection]).area


def measure(baseline: Path, candidate: Path):
    rows = []
    for chapter in range(1, 13):
        filename = f"chapter-{chapter:02}.json"
        _, old_triangles = load_triangles(baseline / filename, chapter)
        data, new_triangles = load_triangles(candidate / filename, chapter)
        old = unary_union(old_triangles).area
        if old <= 0:
            raise ValueError(f"旧章节面积不能为零：{chapter}")
        walk = selected_area(new_triangles, data, "walkReachableTriIndices", candidate / filename)
        reachable = selected_area(new_triangles, data, "anchorReachableTriIndices", candidate / filename)
        rows.append({
            "chapter": chapter,
            "baselineAllNav": round(old, 3),
            "allNav": round(unary_union(new_triangles).area, 1),
            "walk": round(walk, 1),
            "reachable": round(reachable, 1),
            "ratioToConservativeOld": round(reachable / old, 3),
            "meetsTwentyTimes": reachable >= old * 20,
        })
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", type=Path, required=True, help="旧版chapter-01..12.json目录")
    parser.add_argument("--candidate", type=Path, required=True, help="新版chapter-01..12.json目录；不会选-initial文件")
    parser.add_argument("--output", type=Path, help="默认写到candidate/area-comparison.json")
    args = parser.parse_args()
    rows = measure(args.baseline.resolve(strict=True), args.candidate.resolve(strict=True))
    output = args.output if args.output else args.candidate / "area-comparison.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output.resolve()), "chapters": len(rows),
                      "minimumRatio": min(row["ratioToConservativeOld"] for row in rows),
                      "belowTwentyTimes": [row["chapter"] for row in rows if not row["meetsTwentyTimes"]]},
                     ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
