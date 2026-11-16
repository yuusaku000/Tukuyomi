#!/bin/sh
# ツクヨミの exe を作る（世界の組み立て → exe の書き出し、の2回に分けて Unity を動かす）
U="/c/Program Files/Unity/Hub/Editor/6000.1.1f1/Editor/Unity.exe"
P="$(cd "$(dirname "$0")" && pwd -W 2>/dev/null || pwd)"
LOG="${TEMP:-/tmp}/tsukuyomi_logs"; mkdir -p "$LOG"
"$U" -batchmode -quit -projectPath "$P" -executeMethod Tsukuyomi.EditorTools.BuildScript.Prepare -logFile "$LOG/prepare.log" || { echo "prepare failed (see $LOG/prepare.log)"; exit 1; }
"$U" -batchmode -quit -projectPath "$P" -executeMethod Tsukuyomi.EditorTools.BuildScript.BuildOnly -logFile "$LOG/build.log" || { echo "build failed (see $LOG/build.log)"; exit 1; }
grep "\[Tsukuyomi\]" "$LOG/prepare.log" "$LOG/build.log"
