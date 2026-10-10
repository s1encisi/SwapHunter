"""Preview one explicitly selected save COPY. Never imports into formal progress."""
import argparse
from pathlib import Path
import subprocess
import sys
sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")

parser = argparse.ArgumentParser(description="预览旧档副本；不导入正式进度")
parser.add_argument("--copy", required=True, type=Path)
parser.add_argument("--executable", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
parser.add_argument("--exchange-rate")
parser.add_argument("--verified-specialization-spend", type=int)
parser.add_argument("--verified-recall-spend", type=int)
parser.add_argument("--approved-candidate", action="store_true", help="补偿明确且已确认后，仅生成候选副本")
args = parser.parse_args()
copy = args.copy.resolve(strict=True)
exe = args.executable.resolve(strict=True)
output = args.output.resolve()
log = output.with_name(output.name + ".log")
if output.exists() or log.exists():
    parser.error("输出或日志已存在；请用新目录名")
output.parent.mkdir(parents=True, exist_ok=True)
command = [str(exe), "-batchmode", "-migrationCopy", str(copy), "-migrationOutput", str(output), "-logFile", str(log)]
for value, flag in ((args.exchange_rate, "-exchangeRate"), (args.verified_specialization_spend, "-verifiedSpecializationSpend"), (args.verified_recall_spend, "-verifiedRecallSpend")):
    if value is not None:
        command.extend([flag, str(value)])
if args.approved_candidate:
    command.append("-approvedCandidate")
result = subprocess.run(command, cwd=exe.parent, timeout=60, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
if result.returncode:
    raise SystemExit(f"预览未完成，日志：{log}")
print(f"预览已生成：{output}；原副本与正式进度未修改。")
