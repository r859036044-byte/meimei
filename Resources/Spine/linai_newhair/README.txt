新髮型版本（由 妹妹立繪2.psd 的 後髮／側髮／前髮 換進 linai 的 Spine）。
- 原本的 Resources/Spine/linai/ 完全沒動（已固定）。
- 換回舊髮型：把 Scripts/SpineChar.cs 與 Scripts/UI/SpineChar.cs 的 DefaultDir 改回 res://Resources/Spine/linai/
- 換了：後髮_單、側髮左、側髮右、劉海（網格／權重從舊網格最近頂點複製，所以骨骼擺動沿用）。
- 後髮_緞帶（twin 動畫）也改用同一張新後髮（PSD 沒有雙馬尾版，所以雙馬尾造型暫時沒有）。
- 劉海欄位移到「表情」之後，所以前髮畫在眼睛前面。
- 這個 linai.json 是直接改 JSON 產生的（hull/edges 沒填），只給遊戲內的 SpineChar 讀；要回 Spine 編輯器改，請在原專案裡換圖。
