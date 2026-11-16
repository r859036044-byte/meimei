#!/bin/bash
# 一鍵恢復 TrainingRoomRuntime.Dialogue.cs 到修改前版本
cd "$(dirname "$0")/.." && cp Backups/TrainingRoomRuntime.Dialogue.cs.bak Scripts/UI/TrainingRoomRuntime.Dialogue.cs && echo "已恢復"
cp Backups/TrainingRoomRuntime.Voice.cs.bak Scripts/UI/TrainingRoomRuntime.Voice.cs && echo "Voice.cs 已恢復"
cp Backups/SaveLoadController.cs.bak Scripts/Systems/SaveLoadController.cs && echo "SaveLoadController.cs 已恢復"
cp Backups/TrainingRoomRuntime.Help.cs.bak Scripts/UI/TrainingRoomRuntime.Help.cs && echo "Help.cs 已恢復"
cp Backups/TrainingRoomRuntime.Brat.cs.bak Scripts/UI/TrainingRoomRuntime.Brat.cs && cp Backups/SaveLoadHost.cs.bak Scripts/Systems/SaveLoadHost.cs && echo "Brat.cs／SaveLoadHost.cs 已恢復"
cp Backups/GalleryScreen.cs.bak Scripts/UI/GalleryScreen.cs && cp Backups/MainMenuRuntime.Fade.cs.bak Scripts/UI/MainMenuRuntime.Fade.cs && echo "Gallery／Fade 已恢復"
cp Backups/MainMenuRuntime.Intro.cs.bak Scripts/UI/MainMenuRuntime.Intro.cs && echo "Intro 已恢復"
cp Backups/N009.json.bak Resources/Data/Dialogues/Day2/N009.json && echo "N009 已恢復"
cp Backups/TrainingRoomRuntime.PatTalk.cs.bak Scripts/UI/TrainingRoomRuntime.PatTalk.cs && echo "PatTalk 已恢復"
cp Backups/TrainingRoomRuntime.Arousal.cs.bak Scripts/UI/TrainingRoomRuntime.Arousal.cs && cp Backups/TrainingRoomRuntime.Effects.cs.bak Scripts/UI/TrainingRoomRuntime.Effects.cs && cp Backups/TrainingRoomRuntime.Interaction.cs.bak Scripts/UI/TrainingRoomRuntime.Interaction.cs && echo "高潮改動 已恢復"
for f in Brat PatTalk Brow Arousal; do cp Backups/TrainingRoomRuntime.$f.cs.bak3 Scripts/UI/TrainingRoomRuntime.$f.cs; done; rm -f Scripts/UI/TrainingRoomRuntime.PatAnnoy.cs; echo "摸頭不耐煩 已恢復"
