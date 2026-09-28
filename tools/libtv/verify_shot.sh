#!/bin/bash
# 出片验证：产出一张抽帧条和一张「输入场景图 / 输出首帧」上下对比图。
#
#   verify_shot.sh <视频.mp4> <输入场景图> [输出目录]
#
# 抽帧条用来看节拍和人数；对比图用来看风格漂没漂——描线还在吗、
# 建筑有没有被重新打光。风格漂只有叠起来看才发现得了，别靠印象判断。
set -euo pipefail

VIDEO=${1:?用法: verify_shot.sh <视频.mp4> <输入场景图> [输出目录]}
PLATE=${2:?用法: verify_shot.sh <视频.mp4> <输入场景图> [输出目录]}
OUT=${3:-$(dirname "$VIDEO")}

command -v ffmpeg >/dev/null || { echo "需要 ffmpeg" >&2; exit 1; }
mkdir -p "$OUT"

BASE=$(basename "$VIDEO"); BASE=${BASE%.*}
DUR=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$VIDEO")
FPS=$(ffprobe -v error -select_streams v:0 -show_entries stream=r_frame_rate -of csv=p=0 "$VIDEO" | awk -F/ '{print ($2?$1/$2:$1)}')

# 抽 9 帧铺成一条。步长按总帧数算，短片和长片都能铺满。
FRAMES=$(python3 -c "print(int($DUR*$FPS))")
STEP=$(python3 -c "print(max(1, $FRAMES//9))")

ffmpeg -y -v error -i "$VIDEO" \
  -vf "select='not(mod(n\,$STEP))',scale=330:-1,tile=9x1" \
  -frames:v 1 "$OUT/$BASE-抽帧条.jpg"

ffmpeg -y -v error -i "$VIDEO" -frames:v 1 -vf scale=640:-1 "$OUT/.$BASE-f0.jpg"
ffmpeg -y -v error -i "$PLATE" -vf scale=640:-1 "$OUT/.$BASE-plate.jpg"
ffmpeg -y -v error -i "$OUT/.$BASE-plate.jpg" -i "$OUT/.$BASE-f0.jpg" \
  -filter_complex vstack "$OUT/$BASE-首帧对比.jpg"
rm -f "$OUT/.$BASE-f0.jpg" "$OUT/.$BASE-plate.jpg"

printf '%s  %.2fs\n' "$BASE" "$DUR"
echo "  抽帧条   $OUT/$BASE-抽帧条.jpg"
echo "  首帧对比 $OUT/$BASE-首帧对比.jpg  （上=输入场景图，下=输出首帧）"
