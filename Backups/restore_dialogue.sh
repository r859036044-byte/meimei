#!/bin/bash
# 一鍵恢復 TrainingRoomRuntime.Dialogue.cs 到修改前版本
cd "$(dirname "$0")/.." && cp Backups/TrainingRoomRuntime.Dialogue.cs.bak Scripts/UI/TrainingRoomRuntime.Dialogue.cs && echo "已恢復"
