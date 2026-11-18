#!/bin/sh
# ビルドしたもの（Build）を、渡す・遊ぶ用の dist に写す。ツクヨミを閉じてから動かす。
#  ・Tsukuyomi_BurstDebugInformation_DoNotShip（調べもの用のデータ。遊ぶのには要らない）は写さず、dist からも消す
#  ・dist にだけある説明（docs の txt・VRで遊ぶ.bat）は残し、docs の txt は新しいものに置きかえる
cd "$(dirname "$0")" || exit 1
[ -f Build/Tsukuyomi.exe ] || { echo "Build/Tsukuyomi.exe がありません（先に build.sh）"; exit 1; }
mkdir -p dist
for f in Build/*; do
  n=$(basename "$f")
  case "$n" in *_DoNotShip) continue ;; esac
  rm -rf "dist/$n"
  cp -r "$f" "dist/$n"
done
rm -rf dist/*_DoNotShip
cp docs/*.txt dist/
du -sh dist
