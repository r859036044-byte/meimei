using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 全域多國語系管理系統 (I18n)
/// 支援：繁體中文 (zh_TW)、簡體中文 (zh_CN)、英文 (en)、日文 (ja)、韓文 (ko)
/// 提供自動依據角色性格（如傲嬌小惡魔／雌小鬼風格）的在地化翻譯。
/// </summary>
public static class I18n
{
    public static string CurrentLocale { get; set; } = "zh_TW";

    // 各語系字典表
    private static readonly Dictionary<string, Dictionary<string, string>> Dictionaries = new()
    {
        ["zh_CN"] = new Dictionary<string, string>(),
        ["en"]    = new Dictionary<string, string>(),
        ["ja"]    = new Dictionary<string, string>(),
        ["ko"]    = new Dictionary<string, string>()
    };

    static I18n()
    {
        InitTranslations();
    }

    /// <summary>翻譯單一字串，找不到時回傳原字串</summary>
    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;
        if (CurrentLocale == "zh_TW") return key;

        if (Dictionaries.TryGetValue(CurrentLocale, out var dict))
        {
            if (dict.TryGetValue(key, out var val)) return val;

            // 支援愛心 / Emoji 表現型變體容錯匹配
            string norm = key.Replace("\uFE0F", "").Replace("❤️", "❤");
            if (dict.TryGetValue(norm, out val)) return val;
        }
        return key;
    }

    /// <summary>翻譯複合台詞（支援 | 分段與 => 回應語法）</summary>
    public static string TLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        if (CurrentLocale == "zh_TW") return line;

        string reply = null;
        string main = line;
        int arrowIdx = main.IndexOf("=>", StringComparison.Ordinal);
        if (arrowIdx >= 0)
        {
            reply = main.Substring(arrowIdx + 2).Trim();
            main = main.Substring(0, arrowIdx).Trim();
        }

        var segs = main.Split('|');
        for (int i = 0; i < segs.Length; i++)
        {
            segs[i] = T(segs[i].Trim());
        }
        string joined = string.Join("|", segs);

        if (!string.IsNullOrEmpty(reply))
        {
            joined += "=>" + T(reply);
        }
        return joined;
    }

    private static void Add(string key, string zhCN, string en, string ja, string ko)
    {
        string normKey = key.Replace("\uFE0F", "").Replace("❤️", "❤");
        Dictionaries["zh_CN"][key] = zhCN;
        Dictionaries["en"][key]    = en;
        Dictionaries["ja"][key]    = ja;
        Dictionaries["ko"][key]    = ko;

        if (normKey != key)
        {
            Dictionaries["zh_CN"][normKey] = zhCN;
            Dictionaries["en"][normKey]    = en;
            Dictionaries["ja"][normKey]    = ja;
            Dictionaries["ko"][normKey]    = ko;
        }
    }

    public static void RegisterToGodot()
    {
        // 註冊繁體中文 (zh_TW)，映射自身繁體文字，防止 Godot 在找不到 zh_TW 時自動回退 (fallback) 到 zh_CN
        var trTW = new Translation { Locale = "zh_TW" };
        foreach (var key in Dictionaries["zh_CN"].Keys)
        {
            trTW.AddMessage(key, key);
        }
        TranslationServer.AddTranslation(trTW);

        string[] locales = { "zh_CN", "en", "ja", "ko" };
        foreach (var loc in locales)
        {
            var tr = new Translation { Locale = loc };
            foreach (var kvp in Dictionaries[loc])
            {
                tr.AddMessage(kvp.Key, kvp.Value);
            }
            TranslationServer.AddTranslation(tr);
        }
    }

    private static void InitTranslations()
    {
        Add("開始遊戲",
            "开始游戏",
            "Start Game",
            "ゲーム開始",
            "게임 시작");

        Add("繼續遊戲",
            "继续游戏",
            "Continue",
            "続きから",
            "이어하기");

        Add("回想圖鑑",
            "回想图鉴",
            "Gallery",
            "回想図鑑",
            "회상 도감");

        Add("圖　　鑒",
            "图　　鉴",
            "Gallery",
            "回想図鑑",
            "회상 도감");

        Add("圖鑑",
            "图鉴",
            "Gallery",
            "回想図鑑",
            "회상 도감");

        Add("小遊戲",
            "小游戏",
            "Mini Game",
            "ミニゲーム",
            "미니게임");

        Add("遊戲設定",
            "游戏设定",
            "Settings",
            "設定",
            "설정");

        Add("設　　定",
            "设　　定",
            "Settings",
            "設　　定",
            "설　　정");

        Add("設　定",
            "设　定",
            "Settings",
            "設　定",
            "설　정");

        Add("設定",
            "设定",
            "Settings",
            "設定",
            "설정");

        Add("設置",
            "设置",
            "Settings",
            "設定",
            "설정");

        Add("偏好設定",
            "偏好设置",
            "Preferences",
            "環境設定",
            "환경설정");

        Add("退出遊戲",
            "退出游戏",
            "Exit Game",
            "ゲーム終了",
            "게임 종료");

        Add("開場動畫",
            "开场动画",
            "Opening",
            "オープニング",
            "오프닝");

        Add("讀　檔",
            "读　档",
            "Load",
            "ロード",
            "불러오기");

        Add("存　檔",
            "存　档",
            "Save",
            "セーブ",
            "저장하기");

        Add("紀　錄",
            "纪　录",
            "Log",
            "バックログ",
            "대화 기록");

        Add("快　進",
            "快　进",
            "Fast",
            "スキップ",
            "스킵");

        Add("自　動",
            "自　动",
            "Auto",
            "オート",
            "자동 재생");

        Add("跳　過",
            "跳　过",
            "Skip",
            "スキップ",
            "건너뛰기");

        Add("音樂",
            "音乐",
            "Music",
            "音楽",
            "음악");

        Add("音　樂",
            "音　乐",
            "BGM",
            "音楽",
            "음악");

        Add("音樂音量",
            "音乐音量",
            "Music Volume",
            "BGM音量",
            "배경음 음량");

        Add("音效",
            "音效",
            "SFX",
            "効果音",
            "효과음");

        Add("音　效",
            "音　效",
            "SFX",
            "効果音",
            "효과음");

        Add("音效音量",
            "音效音量",
            "SFX Volume",
            "効果音音量",
            "효과음 음량");

        Add("語音",
            "语音",
            "Voice",
            "ボイス",
            "음성");

        Add("語　音",
            "语　音",
            "Voice",
            "ボイス",
            "음성");

        Add("語音音量",
            "语音音量",
            "Voice Volume",
            "ボイス音量",
            "음성 음量");

        Add("語言",
            "语言",
            "Language",
            "言語",
            "언어");

        Add("語　言",
            "语　言",
            "Language",
            "言　語",
            "언　어");

        Add("解析度",
            "分辨率",
            "Resolution",
            "解像度",
            "해상도");

        Add("文字速度",
            "文字速度",
            "Text Speed",
            "文字速度",
            "텍스트 속도");

        Add("顯示模式",
            "显示模式",
            "Display Mode",
            "表示モード",
            "화면 모드");

        Add("視窗模式",
            "窗口模式",
            "Windowed Mode",
            "ウィンドウ設定",
            "화면 모드");

        Add("全螢幕",
            "全屏",
            "Fullscreen",
            "全画面",
            "전체 화면");

        Add("全螢幕模式",
            "全屏模式",
            "Fullscreen",
            "フルスクリーン",
            "전체 화면 모드");

        Add("視窗化",
            "窗口化",
            "Windowed",
            "ウィンドウ",
            "창 모드");

        Add("慢",
            "慢",
            "Slow",
            "遅い",
            "느림");

        Add("普通",
            "普通",
            "Normal",
            "普通",
            "보통");

        Add("快",
            "快",
            "Fast",
            "速い",
            "빠름");

        Add("確定",
            "确定",
            "Confirm",
            "確定",
            "확인");

        Add("套用設定",
            "应用设置",
            "Apply Settings",
            "設定を適用",
            "설정 적용");

        Add("取消",
            "取消",
            "Cancel",
            "キャンセル",
            "취소");

        Add("關　閉",
            "关　闭",
            "Close",
            "閉じる",
            "닫기");

        Add("返回",
            "返回",
            "Back",
            "戻る",
            "돌아가기");

        Add("返　回",
            "返　回",
            "Back",
            "戻る",
            "돌아가기");

        Add("← 返回",
            "← 返回",
            "← Back",
            "← 戻る",
            "← 돌아가기");

        Add("🏠 主畫面",
            "🏠 主界面",
            "🏠 Title",
            "🏠 タイトル",
            "🏠 메인 화면");

        Add("選擇解析度會切換至視窗模式；全螢幕使用螢幕原生大小。",
            "选择分辨率会切换至窗口模式；全屏使用屏幕原生大小。",
            "Selecting a resolution switches to windowed mode; fullscreen uses native display resolution.",
            "解像度を選択するとウィンドウ表示になります。全画面時はネイティブ解像度になります。",
            "해상도를 선택하면 창 모드로 전환되며, 전체 화면에서는 모니터 기본 해상도를 사용합니다.");

        Add("手・舌透明度",
            "手・舌透明度",
            "Hand/Tongue Alpha",
            "手・舌の透明度",
            "손/혀 투명도");

        Add("全年齡",
            "全年龄",
            "All-Ages",
            "全年齢",
            "전연령");

        Add("全年齡模式",
            "全年龄模式",
            "All-Ages Mode",
            "全年齢モード",
            "전연령 모드");

        Add("使用全年齡模式（勾選為非 R18）",
            "使用全年龄模式（勾选为非 R18）",
            "All-Ages Mode (Check for Non-R18)",
            "全年齢モードを使用（チェックで非R18）",
            "전연령 모드 사용 (체크 시 비R18)");

        Add("使用全年齡模式（取消勾選為 R18 版）",
            "使用全年龄模式（取消勾选为 R18 版）",
            "All-Ages Mode (Uncheck for R18)",
            "全年齢モードを使用（チェック解除でR18）",
            "전연령 모드 사용 (체크 해제 시 R18)");

        Add("確認退出？",
            "确认退出？",
            "Quit Game?",
            "ゲームを終了しますか？",
            "게임을 종료하시겠습니까?");

        Add("是否要進入序章？",
            "是否要进入序章？",
            "Play Prologue?",
            "プロローグを見ますか？",
            "프롤로그를 보시겠습니까?");

        Add("要先看看故事的開始嗎？",
            "要先看看故事的开始吗？",
            "Would you like to watch the opening prologue?",
            "物語の始まりを見ますか？",
            "스토리의 시작을 보시겠습니까?");

        Add("慢慢讀完序章，或直接開始遊戲",
            "慢慢读完序章，或直接开始游戏",
            "Read through prologue or jump straight in",
            "プロローグを読むか、直接始めるか選べます",
            "프롤로그를 읽거나 바로 게임을 시작할 수 있습니다");

        Add("進入序章",
            "进入序章",
            "Watch Prologue",
            "プロローグへ",
            "프롤로그 보기");

        Add("觀看劇情",
            "观看剧情",
            "Watch Prologue",
            "プロローグを見る",
            "프롤로그 감상");

        Add("直接開始",
            "直接开始",
            "Skip to Game",
            "直接開始",
            "바로 시작");

        Add("覆蓋存檔",
            "覆盖存档",
            "Overwrite Save",
            "セーブ上書き",
            "세이브 덮어쓰기");

        Add("左手",
            "左手",
            "Left Hand",
            "左手",
            "왼손");

        Add("右手",
            "右手",
            "Right Hand",
            "右手",
            "오른손");

        Add("滑鼠",
            "鼠标",
            "Mouse",
            "マウス",
            "마우스");

        Add("衣服模式",
            "衣服模式",
            "Clothes Mode",
            "服モード",
            "옷 모드");

        Add("舌頭",
            "舌头",
            "Tongue",
            "舌",
            "혀");

        Add("忍耐",
            "忍耐",
            "Endure",
            "我慢",
            "인내");

        Add("離開房間",
            "离开房间",
            "Leave Room",
            "部屋を出る",
            "방 나가기");

        Add("選單",
            "菜单",
            "Menu",
            "メニュー",
            "메뉴");

        Add("截圖",
            "截图",
            "Screenshot",
            "スクリーンショット",
            "스크린샷");

        Add("操作說明",
            "操作说明",
            "How to Play",
            "操作説明",
            "조작 설명");

        Add("衣服模式（只拖衣服／被子，忽略綠點）",
            "衣服模式（只拖衣服／被子，忽略绿点）",
            "Clothes Mode (Drag clothes/blanket only)",
            "服モード（服や布団のみ移動）",
            "옷 모드 (옷/이불만 조작)");

        Add("滑鼠（預設操作；選其他工具時取消選取）",
            "鼠标（默认操作；选其他工具时取消选取）",
            "Mouse (Default controls)",
            "マウス（基本操作に戻る）",
            "마우스 (기본 조작)");

        Add("舌頭（選取後點嘴巴）",
            "舌头（选取后点嘴巴）",
            "Tongue (Select then click mouth)",
            "舌（選択後に口をクリック）",
            "혀 (선택 후 입 클릭)");

        Add("忍耐（精力快滿時按，可以忍回去；抽插太快會忍不住）",
            "忍耐（精力快满时按，可以忍回去）",
            "Endure (Hold back before climax)",
            "我慢（限界が近い時に我慢する）",
            "인내 (한계 직전에 버티기)");

        Add("離開房間（結束今天）",
            "离开房间（结束今天）",
            "Leave Room (End Day)",
            "部屋を出る（今日を終了）",
            "방 나가기 (오늘 종료)");

        Add("射精（精力夠高才能按）",
            "射精（精力够高才能按）",
            "Ejaculate (Requires high stamina)",
            "射精（体力が高い時のみ可能）",
            "사정 (정력이 높을 때 가능)");

        Add("精力（男主感覺，90 可射）",
            "精力（男主感觉，90 可射）",
            "Stamina (Arousal level, 90 to cum)",
            "体力（興奮度、90で射精可）",
            "정력 (흥분도, 90 이상 사정 가능)");

        Add("雞雞（點擊叫出／收起；叫出後顯示精力）",
            "肉棒（点击唤出／收起；唤出后显示精力）",
            "Cock (Click to show/hide)",
            "チ○ポ（クリックで出す／しまう）",
            "자지 (클릭으로 꺼내기/집어넣기)");

        Add("♡ 狀態",
            "♡ 状态",
            "♡ Status",
            "♡ ステータス",
            "♡ 상태");

        Add("狀態",
            "状态",
            "Status",
            "ステータス",
            "상태");

        Add("高潮度",
            "高潮度",
            "Climax",
            "絶頂度",
            "절정도");

        Add("💗 高潮度",
            "💗 高潮度",
            "💗 Climax",
            "💗 絶頂度",
            "💗 절정도");

        Add("心情",
            "心情",
            "Mood",
            "機嫌",
            "기분");

        Add("舒服度",
            "舒适度",
            "Comfort",
            "快感度",
            "쾌감도");

        Add("嘴巴舒服",
            "嘴巴舒适",
            "Mouth Comfort",
            "口 快感",
            "입 쾌감");

        Add("胸部舒服",
            "胸部舒适",
            "Chest Comfort",
            "胸 快感",
            "가슴 쾌감");

        Add("尻部舒服",
            "尻部舒适",
            "Hip Comfort",
            "お尻 快感",
            "엉덩이 쾌감");

        Add("小穴舒服",
            "下体舒适",
            "Intimate Comfort",
            "秘部 快感",
            "비부 쾌감");

        Add("雌小鬼",
            "雌小鬼",
            "Brat Level",
            "メスガキ度",
            "메스가키도");

        Add("雌小鬼互動",
            "雌小鬼互动",
            "Brat Interaction",
            "メスガキ対応",
            "메스가키 반응");

        Add("精力",
            "精力",
            "Stamina",
            "体力",
            "정력");

        Add("精液量",
            "精液量",
            "Semen",
            "ザーメン量",
            "사정량");

        Add("濕度",
            "湿度",
            "Wetness",
            "潤い",
            "젖음도");

        Add("勃起度",
            "勃起度",
            "Erection",
            "勃起度",
            "발기도");

        Add("調教室",
            "调教室",
            "Training Room",
            "調教室",
            "조교실");

        Add("流程",
            "流程",
            "Flow",
            "フロー",
            "진행");

        Add("結束今晚",
            "结束今晚",
            "End Tonight",
            "今夜を終える",
            "오늘 밤 종료");

        Add("快速操作",
            "快速操作",
            "Quick Actions",
            "クイック操作",
            "빠른 조작");

        Add("✋ 摸了",
            "✋ 摸了",
            "✋ Touch",
            "✋ 触る",
            "✋ 만지기");

        Add("💗 高潮",
            "💗 高潮",
            "💗 Climax",
            "💗 絶頂",
            "💗 절정");

        Add("🍑 插入",
            "🍑 插入",
            "🍑 Insert",
            "🍑 挿入",
            "🍑 삽입");

        Add("💦 射精",
            "💦 射精",
            "💦 Ejaculate",
            "💦 射精",
            "💦 사정");

        Add("✕ 清精液",
            "✕ 清理",
            "✕ Clean",
            "✕ 拭き取る",
            "✕ 정액 닦기");

        Add("綠點游標變化",
            "绿点光标变化",
            "Green Dot Cursor Change",
            "緑カーソル変化",
            "녹색 점 커서 변화");

        Add("懸停提示氣泡",
            "悬停提示气泡",
            "Hover Tooltips",
            "ツールチップ表示",
            "툴팁 말풍선");

        Add("劇情觸發",
            "剧情触发",
            "Story Triggers",
            "シナリオ進行",
            "스토리 트리거");

        Add("D2 首次進場",
            "D2 首次进场",
            "D2 First Entry",
            "D2 初入場",
            "D2 첫 입장");

        Add("D3 首次進場",
            "D3 首次进场",
            "D3 First Entry",
            "D3 初入場",
            "D3 첫 입장");

        Add("第三晚觸發",
            "第三晚触发",
            "Night 3 Trigger",
            "3日目の夜",
            "3일차 밤");

        Add("摸胸已發生",
            "摸胸已发生",
            "Chest Touched",
            "胸タッチ済み",
            "가슴 만짐 플래그");

        Add("舒服路線",
            "舒服路线",
            "Pleasure Route",
            "快感ルート",
            "쾌감 루트");

        Add("小遊戲直入",
            "小游戏直入",
            "Direct Mini Game",
            "ミニゲーム直行",
            "미니게임 직행");

        Add("要怎麼回應她？",
            "要怎么回应她？",
            "How to respond to her?",
            "どう返す？",
            "어떻게 반응할까?");

        Add("教訓她",
            "教训她",
            "Teach her a lesson",
            "わからせる",
            "참교육하기");

        Add("好好教訓這個雌小鬼",
            "好好教训这个小鬼",
            "Discipline this brat",
            "生意気な小娘をわからせる",
            "메스가키 참교육하기");

        Add("讓她知道厲害",
            "让她知道厉害",
            "Show her who's boss",
            "思い知らせてやる",
            "본때를 보여주기");

        Add("懲罰這個雌小鬼",
            "惩罚这个小鬼",
            "Punish this brat",
            "お仕置きする",
            "건방진 꼬맹이 벌주기");

        Add("不理她",
            "不理她",
            "Ignore her",
            "無視する",
            "무시하기");

        Add("裝作沒聽到",
            "装作没听到",
            "Pretend not to hear",
            "聞こえないふり",
            "못 들은 척하기");

        Add("隨她去說",
            "随她去说",
            "Let her babble",
            "放っておく",
            "멋대로 지껄이게 두기");

        Add("繼續做自己的",
            "继续做自己的",
            "Keep going",
            "そのまま続ける",
            "계속하기");

        Add("溫柔一點",
            "温柔一点",
            "Be gentle",
            "優しくする",
            "상냥하게 하기");

        Add("慢慢來",
            "慢慢来",
            "Take it slow",
            "ゆっくり行く",
            "천천히 하기");

        Add("聽她的，放慢一點",
            "听她的，放慢一点",
            "Slow down for her",
            "言う通りゆっくりにする",
            "말대로 천천히 하기");

        Add("繼續",
            "继续",
            "Continue",
            "続ける",
            "계속");

        Add("不用管，繼續",
            "不用管，继续",
            "Don't mind her, keep going",
            "構わず続ける",
            "신경 끄고 계속");

        Add("就這樣",
            "就这样",
            "Just like this",
            "このまま",
            "이대로");

        Add("小鬼，這就是你的懲罰",
            "小鬼，这就是你的惩罚",
            "Brat, this is your punishment",
            "お仕置きの時間だ",
            "꼬맹아, 이게 네 벌이다");

        Add("叫你亂說話，這是懲罰",
            "叫你乱说话，这是惩罚",
            "Punishment for your cheeky mouth",
            "生意気言った罰だ",
            "까분 대가다");

        Add("再嘴硬就別怪我了",
            "再嘴硬就别怪我了",
            "Keep talking and see what happens",
            "まだ減らず口を叩く気か",
            "더 뻐겨봐라, 가만 안 둔다");

        Add("慢下來吧",
            "慢下来吧",
            "Slow down",
            "ペースを落とす",
            "속도 줄이기");

        Add("好吧，放慢一點",
            "好吧，放慢一点",
            "Fine, a little slower",
            "わかった、ゆっくりにする",
            "알았어, 천천히 할게");

        Add("別急，慢慢來",
            "别急，慢慢来",
            "No rush, nice and slow",
            "焦らずゆっくり行こう",
            "급할 거 없어, 천천히 하자");

        Add("別停，就這樣",
            "别停，就这样",
            "Don't stop, like this",
            "止めるな、このまま",
            "멈추지 마, 이대로");

        Add("繼續，不要停",
            "继续，不要停",
            "Keep going, don't stop",
            "続けて、止まらないで",
            "계속해, 멈추지 말고");

        Add("就這樣繼續",
            "就这样继续",
            "Keep going just like this",
            "この調子で続ける",
            "이대로 계속 가자");

        Add("她喊痛了…",
            "她喊痛了…",
            "She's hurting…",
            "痛がっている…",
            "아파하고 있어…");

        Add("她快要不行了…",
            "她快要不行了…",
            "She's at her limit…",
            "もう限界のようだ…",
            "한계인 것 같아…");

        Add("射在裡面",
            "射在里面",
            "Inside",
            "中に注ぐ",
            "안에 싸기");

        Add("射在外面",
            "射在外面",
            "Outside",
            "外に出す",
            "밖에 싸기");

        Add("對、對不起～！",
            "对、对不起～！",
            "I-I'm sorry~!",
            "ご、ごめんなさいぃ〜っ！",
            "죄, 죄송해요~!");

        Add("我錯了啦～！",
            "我错了啦～！",
            "I was wrong, please~!",
            "私が悪かったよぉ〜っ！",
            "내가 잘못했어~!");

        Add("慢、慢一點…求你了…！",
            "慢、慢一点…求你了…！",
            "S-Slow down… I beg you…!",
            "ゆ、ゆっくりにしてぇ…お願いぃ…！",
            "처, 천천히 해줘… 제발…!");

        Add("……嗯。",
            "……嗯。",
            "……Mm.",
            "……んっ。",
            "……응.");

        Add("哼…這次就放過你。",
            "哼…这次就放过你。",
            "Hmph… I'll let you off this time.",
            "ふん…今回だけ許してあげる。",
            "흥… 이번만 봐주는 거야.");

        Add("…呼。",
            "…呼。",
            "…Phew.",
            "…ふぅ。",
            "…후우.");

        Add("痛痛痛～！",
            "痛痛痛～！",
            "Ow ow ouch~!",
            "痛い痛い痛ぁい〜っ！",
            "아파 아파 아프다고~!");

        Add("嗚…",
            "呜…",
            "Ugh…",
            "うぅ…",
            "으으…");

        Add("放手啦～！",
            "放手啦～！",
            "Let go of me~!",
            "手離してよぉ〜っ！",
            "손 놔~!");

        Add("…你幹嘛？",
            "…你干嘛？",
            "…What are you doing?",
            "…何するの？",
            "…뭐 하는 거야?");

        Add("好痛…",
            "好痛…",
            "It hurts…",
            "痛いよ…",
            "아파…");

        Add("……",
            "……",
            "……",
            "……",
            "……");

        Add("呼……❤就只有這點本事嗎？",
            "呼……❤就只有这点本事吗？",
            "Phew……❤ Is that all you've got?",
            "ふぅ……❤この程度しかできないのぉ〜？",
            "후우……❤ 겨우 이 정도 실력이야~?");

        Add("哈啊❤…再來…啊哈…❤",
            "哈啊❤…再来…啊哈…❤",
            "Haah❤… More… Ahaa…❤",
            "はぁっ❤……もっとぉ……あはっ……❤",
            "하아❤… 더 해줘… 아하…❤");

        Add("好舒服❤……再來…❤我還想要❤",
            "好舒服❤……再来…❤我还想要❤",
            "Feels so good❤… More…❤ I want more❤",
            "すごく気持ちいい……❤もっと……❤もっと欲しいのぉ……❤",
            "너무 기분 좋아……❤ 더……❤ 나 더 원해……❤");

        Add("嗯❤……感覺身體變得好奇怪❤",
            "嗯❤……感觉身体变得好奇怪❤",
            "Nnh❤… My body feels so weird❤",
            "んんっ❤……身体がなんだかおかしくなっちゃうぅ❤",
            "으응❤…… 몸이 왠지 이상해지는 것 같아❤");

        Add("啊❤……那裡❤喜歡❤……",
            "啊❤……那里❤喜欢❤……",
            "Ahh❤… Right there❤… I love it❤…",
            "あっ❤……そこぉ❤好きぃ……❤",
            "아앗❤…… 거기❤ 좋아❤……");

        Add("哼～雜魚❤～",
            "哼～杂鱼❤～",
            "Hmph~ Loser❤~",
            "ふんっ、ざぁ〜こ❤",
            "흥~ 허접❤~");

        Add("～弱雞❤～",
            "～弱鸡❤～",
            "~Weakling❤~",
            "〜よわよわ❤〜",
            "~약골❤~");

        Add("這種雜魚也敢命令我嗎？",
            "这种杂鱼也敢命令我吗？",
            "A scrub like you thinks he can order me around?",
            "こんなざこ兄ちゃんが私に指図する気ぃ〜？",
            "이런 허접이 감히 나한테 명령이야~?");

        Add("笨蛋笨蛋～",
            "笨蛋笨蛋～",
            "Dummy, dummy~",
            "ばぁ〜か、ばぁ〜か❤",
            "바보 바보~❤");

        Add("才不聽你的呢～",
            "才不听你的呢～",
            "I'm not listening to you~",
            "言うことなんか聞いてあげないもんね〜",
            "오빠 말 따위 들을까 보냐~");

        Add("雜魚好沒用～",
            "杂鱼好没用～",
            "Loser's totally useless~",
            "ざこ兄ちゃん使えない〜❤",
            "허접 오빠 쓸모없어~❤");

        Add("喂～我都等好久了耶～",
            "喂～我都等好久了耶～",
            "Hey~ I've been waiting forever~",
            "ねぇ〜、ずっと待たせてるんですけどぉ〜？",
            "저기~ 나 엄청 오래 기다렸거든~?");

        Add("你是不是在發呆啊～？",
            "你是不是在发呆啊～？",
            "Are you daydreaming or what~?",
            "ボーッとしてんじゃないわよぉ〜？",
            "멍 때리고 있는 거 아니지~?");

        Add("真是的，一點用都沒有～",
            "真是的，一点用都没有～",
            "Sheesh, totally useless~",
            "まったく、頼りないんだからぁ〜",
            "정말이지, 도움 안 된다니까~");

        Add("要我誇你嗎？才不要～",
            "要我夸你吗？才不要～",
            "Want me to praise you? Not a chance~",
            "褒めてほしいのぉ？ぜーったい嫌っ❤",
            "칭찬이라도 바래? 절대 안 해~❤");

        Add("雜魚的臉好呆喔～",
            "杂鱼的脸好呆喔～",
            "You have such a goofy face, loser~",
            "ざこ兄ちゃんマヌケな顔してる〜❤",
            "허접 오빠 표정 진짜 멍청해~❤");

        Add("哼，別以為我會乖乖聽話～",
            "哼，别以为我会乖乖听话～",
            "Hmph, don't think I'll just obey you~",
            "ふんっ、素直に従うと思ったら大間違いよ！",
            "흥, 고분고분 들을 줄 알고 착각 마시지!");

        Add("睡覺的時候還是挺可愛的嘛~",
            "睡觉的时候还是挺可爱得嘛~",
            "You're pretty cute when you're sleeping~",
            "寝てる時は案外可愛いところあるじゃん〜",
            "자고 있을 때는 제법 귀엽단 말이지~");

        Add("嗯……",
            "嗯……",
            "Mm……",
            "んん……",
            "으응……");

        Add("睡覺還是雌小鬼的樣子嗎？",
            "睡觉还是雌小鬼的样子吗？",
            "Are you still a brat even while sleeping?",
            "寝てる時までメスガキのままなのかよ？",
            "자면서까지 메스가키 모습인 거야?");

        Add("我哪有命令你，只是問問你要不要而已。",
            "我哪有命令你，只是问问你要不要而已。",
            "I'm not ordering you, just asking if you'd like to.",
            "命令なんてしてないだろ、欲しいか聞いただけで。",
            "명령한 적 없거든, 그냥 원하는지 물어본 것뿐이야.");

        Add("今天辛苦了。",
            "今天辛苦了。",
            "Thanks for your hard work today.",
            "今日もお疲れ様でした。",
            "오늘 하루 수고 많았어.");

        Add("謝謝你。",
            "谢谢你。",
            "Thank you.",
            "ありがとうね。",
            "고마워.");

        Add("嗯，我知道了。",
            "嗯，我知道了。",
            "Mm, I understand.",
            "うん、わかった。",
            "응, 알았어.");

        Add("要不要一起吃點東西？",
            "要不要一起吃点东西？",
            "Want to grab a bite together?",
            "一緒に何か食べない？",
            "같이 뭐 좀 먹을래?");

        Add("今天天氣不錯呢。",
            "今天天气不错呢。",
            "The weather is really nice today.",
            "今日はいいてんきだね。",
            "오늘 날씨 참 좋다.");

        Add("你今天有好好吃飯嗎？",
            "你今天有好好吃饭吗？",
            "Did you eat properly today?",
            "今日ちゃんとご飯食べた？",
            "오늘 밥은 잘 챙겨 먹었어?");

        Add("……我有點睏了。",
            "……我有点困了。",
            "……I'm feeling a bit sleepy.",
            "……ちょっと眠くなっちゃった。",
            "……나 조금 졸려.");

        Add("那個，明天要做什麼呢？",
            "那个，明天要做什么呢？",
            "Um, what are we doing tomorrow?",
            "あのね、明日は何するの？",
            "저기, 내일은 뭐 해?");

        Add("有你在，我比較安心。",
            "有你在，我比较安心。",
            "I feel more at ease with you here.",
            "お兄ちゃんがいると安心するよ。",
            "오빠가 있으니까 안심돼.");

        Add("要不要早點休息？",
            "要不要早点休息？",
            "Should we call it a night early?",
            "早めに休む？",
            "일찍 쉴래?");

        Add("哼～看什麼看啦，雜魚～",
            "哼～看什么看啦，杂鱼～",
            "Hmph~ What are you staring at, loser~?",
            "ふんっ、何ジロジロ見てるのよ、ざぁ〜こ❤",
            "흥~ 뭘 빤히 쳐다봐, 허접 오빠~?❤");

        Add("衣服都亂掉了～變態～",
            "衣服都乱掉了～变态～",
            "My clothes are a mess now~ Pervert~",
            "服が乱れちゃったじゃん〜、へんたい❤",
            "옷 다 흐트러졌잖아~ 변태❤");

        Add("這麼想看喔？笨蛋～",
            "这么想看喔？笨蛋～",
            "Wanna look that badly? Dummy~",
            "そんなに見たいわけぇ？ばぁ〜か❤",
            "그렇게나 보고 싶어? 바보~❤");

        Add("眼睛都直了喔～好噁心～",
            "眼睛都直了喔～好恶心～",
            "Your eyes are glued to me~ Gross~",
            "目が釘付けじゃん〜キモ〜い❤",
            "눈 돌아갔네~ 완전 징그러~❤");

        Add("再看我就收費囉～",
            "再看我就收费啰～",
            "Keep staring and I'll charge you~",
            "これ以上見たら有料だからね〜？",
            "더 쳐다보면 요금 받을 거다~?");

        Add("才、才不是故意讓你看的～",
            "才、才不是故意让你看的～",
            "I-It's not like I wanted you to look~",
            "べ、別に見てほしくてこうしてるんじゃないんだからね！",
            "벼, 별로 오빠 보여주려고 이런 거 아니거든!");

        Add("雜魚果然是變態～",
            "杂鱼果然是变态～",
            "Such a pervert, loser~",
            "ざこ兄ちゃんやっぱり変態だぁ〜❤",
            "허접 오빠 역시 변태였어~❤");

        Add("哼，只准看，不准亂來～",
            "哼，只准看，不准乱来～",
            "Hmph, you can look, but hands off~",
            "ふん、見るだけよ、手出しは禁止だからね！",
            "흥, 보는 것만 돼! 손 대면 가만 안 둬!");

        Add("臉都紅了耶～純情雜魚～",
            "脸都红了耶～纯情杂鱼～",
            "Your whole face is red~ Pure little loser~",
            "顔真っ赤じゃん〜純情ざこ兄ちゃん❤",
            "얼굴 새빨개졌네~ 순진한 허접 오빠❤");

        Add("你的視線好煩喔～",
            "你的视线好烦喔～",
            "Your staring is so annoying~",
            "その視線、すっごくうっとうしい〜！",
            "그 시선, 완전 성가시거든~!");

        Add("……衣服，有點涼呢。",
            "……衣服，有点凉呢。",
            "……The clothes feel a bit chilly.",
            "……服がはだけて、ちょっとスースーする。",
            "……옷이 벗겨져서 조금 쌀쌀하네.");

        Add("那個…可以幫我整理一下嗎？",
            "那个…可以帮我整理一下吗？",
            "Um… could you help tidy me up?",
            "あの…服直してくれないかな…？",
            "저기… 옷 좀 정리해 줄 수 있어?");

        Add("你、你在看哪裡啦…",
            "你、你在看哪里啦…",
            "Wh-Where are you looking…",
            "ど、どこ見てるのよぉ…",
            "어, 어디를 보고 있는 거야…");

        Add("有點不好意思…",
            "有点不好意思…",
            "It's a bit embarrassing…",
            "ちょっと恥ずかしいかも…",
            "조금 부끄러워…");

        Add("可以…把被子拉上來嗎？",
            "可以…把被子拉上来吗？",
            "Could you… pull up the blanket?",
            "お布団…かけてくれる…？",
            "이불… 덮어줄래…?");

        Add("不要一直盯著看啦……",
            "不要一直盯着看啦……",
            "Don't keep staring like that……",
            "あんまりジロジロ見ないでよぉ……",
            "그렇게 빤히 쳐다보지 마……");

        Add("感覺有點冷。",
            "感觉有点冷。",
            "I feel a bit cold.",
            "ちょっと寒いかも。",
            "조금 추운 것 같아.");

        Add("我、我自己來整理就好…",
            "我、我自己来整理就好…",
            "I-I can fix it myself…",
            "じ、自分で直すから大丈夫…！",
            "나, 내가 직접 정리할 테니까…!");

        Add("……不要看啦。",
            "……不要看啦。",
            "……Don't look.",
            "……見ないでよぉ。",
            "……보지 마.");

        Add("身體好像有點熱熱的。",
            "身体好像有点热热的。",
            "My body feels kind of warm.",
            "身体がなんだか熱いかも…。",
            "몸이 왠지 달아오르는 것 같아….");

        Add("哼嗯…這種程度…才不算什麼～",
            "哼嗯…这种程度…才不算什么～",
            "Hmph… this is nothing… at all~",
            "ふんっ…このくらい…へっちゃらなんだからぁ〜",
            "흥… 이 정도쯤이야… 아무렇지도 않거든~");

        Add("不、不要得意忘形喔～！",
            "不、不要得意忘形喔～！",
            "D-Don't get cocky~!",
            "調子に乗らないでよねぇ〜っ！",
            "우, 우쭐대지 마시지~!");

        Add("嗯…才沒有很舒服啦～",
            "嗯…才没有很舒服啦～",
            "Nnh… It doesn't even feel good~",
            "んん…き、気持ちよくなんてないんだからぁ…",
            "으응… 기, 기분 좋은 거 아니거든~");

        Add("雜魚…別、別太用力～",
            "杂鱼…别、别太用力～",
            "Loser… D-Don't push too hard~",
            "ざこ兄ちゃん…む、無理しないでよぉ…",
            "허접 오빠… 너, 너무 세게 하지 마~");

        Add("嘴上說不要…身體才沒有誠實呢～",
            "嘴上说不要…身体才没有诚实呢～",
            "Saying no… but your body isn't honest at all~",
            "口では嫌がっても…身体は正直じゃないもんね〜",
            "입으로는 싫다면서… 몸은 솔직하지도 않네~");

        Add("哼…你、你別笑啊～",
            "哼…你、你别笑啊～",
            "Hmph… d-don't laugh at me~",
            "ふんっ…わ、笑わないでよぉ〜",
            "흥… 우, 웃지 마시지~");

        Add("才、才不會輸給你～",
            "才、才不会输给你～",
            "I-I'm not losing to you~",
            "ま、負けたりしないんだからぁ〜！",
            "지, 질까 보냐~!");

        Add("這、這點小事…！",
            "这、这点小事…！",
            "Th-This is nothing…!",
            "こ、こんなの平気だしぃ…！",
            "이, 이 정도쯤은…!");

        Add("再…再快一點也沒關係喔～…才怪～",
            "再…再快一点也没关系喔～…才怪～",
            "You could go a bit faster~… As if~!",
            "も、もっと速くてもいいよぉ〜…なーんてね❤",
            "조, 조금 더 빨리 해도 괜찮거든~… 농담이지만~❤");

        Add("喂…不許看我的臉啦～",
            "喂…不许看我的脸啦～",
            "Hey… don't look at my face~",
            "ねぇ…私の顔見ないでよぉ〜っ",
            "야… 내 얼굴 보지 마~");

        Add("嗯…慢一點…",
            "嗯…慢一点…",
            "Nnh… a bit slower…",
            "んん…ゆっくりぃ…",
            "으응… 천천히…");

        Add("……呼…",
            "……呼…",
            "……Haah…",
            "……ふぅ…",
            "……후우…");

        Add("啊…有點…奇怪的感覺…",
            "啊…有点…奇怪的感觉…",
            "Ah… such a… strange feeling…",
            "あっ…なんか…変な感じ…",
            "아앗… 뭔가… 이상한 기분…");

        Add("不要…停下來…",
            "不要…停下来…",
            "Don't… stop…",
            "止めない…でぇ…",
            "멈추지… 마…");

        Add("嗯…抱緊我…",
            "嗯…抱紧我…",
            "Mm… hold me tight…",
            "ぎゅって…抱きしめて…",
            "꼭… 안아줘…");

        Add("……哥哥…",
            "……哥哥…",
            "……Big brother…",
            "……お兄ちゃん…",
            "……오빠…");

        Add("呼…呼…有點喘不過氣。",
            "呼…呼…有点喘不过气。",
            "Haah… haah… taking my breath away.",
            "はぁ…はぁ…息が苦しいかも…",
            "하아… 하아… 숨이 차…");

        Add("嗯…可以…再靠近一點。",
            "嗯…可以…再靠近一点。",
            "Mm… you can… come a little closer.",
            "ん…もっと…くっついていいよ…",
            "응… 좀 더… 가까이 와도 돼…");

        Add("……好熱。",
            "……好热。",
            "……So hot.",
            "……すごく熱いよ。",
            "……너무 뜨거워.");

        Add("唔…不要太急…",
            "唔…不要太急…",
            "Nnh… don't rush so much…",
            "んぅ…焦らないでぇ…",
            "으응… 너무 서두르지 마…");

        Add("痛、痛啦～你不會輕一點嗎～",
            "痛、痛啦～你不会轻一点吗～",
            "Ouch, it hurts~ Can't you be gentler~?",
            "痛いっ、痛いよぉ〜！もっと優しくできないのぉ〜？",
            "아파, 아프다고~! 좀 살살 못 해~?");

        Add("笨蛋雜魚～弄得人家好痛～",
            "笨蛋杂鱼～弄得人家好痛～",
            "Dummy loser~ That really hurts~",
            "ばかざこ兄ちゃん！痛くしないでよぉ〜！",
            "바보 허접 오빠! 아프게 만들지 마~!");

        Add("哼…這樣一點都不舒服～",
            "哼…这样一点都不舒服～",
            "Hmph… this doesn't feel good at all~",
            "ふんっ…全然気持ちよくないんだから！",
            "흥… 하나도 기분 안 좋거든!");

        Add("速度不對啦～雜魚真的很笨～",
            "速度不对啦～杂鱼真的很笨～",
            "Your pacing's all wrong~ You're so clumsy, loser~",
            "リズムが変！ざこ兄ちゃんほんと下手くそ〜！",
            "박자가 안 맞잖아! 허접 오빠 진짜 못해~!");

        Add("好痛…你是故意的吧～",
            "好痛…你是故意的吧～",
            "It hurts… you're doing that on purpose, aren't you~?",
            "痛ぁい…わざとやってるでしょぉ〜！",
            "아파… 일부러 그러는 거지~?");

        Add("嗚…別、別那麼用力啦～",
            "呜…别、别那么用力啦～",
            "Ugh… d-don't push so hard~",
            "うぅ…そ、そんなに力入れないでよぉ〜",
            "으으… 너, 너무 힘주지 마~");

        Add("這種技術也敢上場喔～？",
            "这种技术也敢上场喔～？",
            "With skills like that, you dared to try~?",
            "そんなヘタクソでよく挑んできたわね〜？",
            "그런 엉터리 솜씨로 덤벼든 거야~?");

        Add("痛痛痛…我要生氣囉～",
            "痛痛痛…我要生气啰～",
            "Ow ow ow… I'm gonna get mad~",
            "痛い痛い…怒っちゃうからね〜っ！",
            "아파 아파… 나 진짜 화낼 거야~!");

        Add("唔…有點痛…",
            "唔…有点痛…",
            "Nnh… it hurts a little…",
            "んぅ…ちょっと痛いかも…",
            "으응… 조금 아파…");

        Add("太快了…慢一點",
            "太快了…慢一点",
            "Too fast… slow down a bit",
            "速すぎる…ゆっくりにして",
            "너무 빨라… 천천히 해줘");

        Add("抱歉，我放慢一點。",
            "抱歉，我放慢一点。",
            "Sorry, I'll take it slower.",
            "ごめん、ゆっくりにするよ。",
            "미안, 천천히 할게.");

        Add("……慢一點，拜託。",
            "……慢一点，拜托。",
            "……Slower, please.",
            "……ゆっくり、お願い。",
            "……천천히, 부탁이야.");

        Add("那裡…不舒服…",
            "那里…不舒服…",
            "Right there… it doesn't feel right…",
            "そこ…痛いよぉ…",
            "거기… 불편해…");

        Add("不要…這樣會痛…",
            "不要…这样会痛…",
            "No… that hurts…",
            "やだ…それ痛いもん…",
            "싫어… 그거 아프단 말이야…");

        Add("請、請輕一點…",
            "请、请轻一点…",
            "Pl-Please be gentler…",
            "お、お願い…優しくして…",
            "제, 제발… 살살 해줘…");

        Add("……我有點受不了。",
            "……我有点受不了。",
            "……I can't take much more.",
            "……もう我慢できないかも。",
            "……더는 못 버틸 것 같아.");

        Add("好好好，我慢一點。",
            "好好好，我慢一点。",
            "Alright, alright, I'll go slower.",
            "はいはい、ゆっくりするから。",
            "알았어, 알았어. 천천히 할게.");

        Add("抱歉抱歉，是不是太快了？",
            "抱歉抱歉，是不是太快了？",
            "Sorry about that, was it too fast?",
            "ごめんごめん、速すぎたか？",
            "미안미안, 너무 빨랐지?");

        Add("別逞強啊，痛就說。",
            "别逞强啊，痛就说。",
            "Don't push yourself, speak up if it hurts.",
            "無理するなよ、痛いなら言え。",
            "무리하지 마, 아프면 아프다고 말해.");

        Add("那我換個節奏。",
            "那我换个节奏。",
            "I'll change the rhythm then.",
            "じゃあテンポ変えるよ。",
            "그럼 속도를 좀 바꿀게.");

        Add("對不起，我會輕一點。",
            "对不起，我会轻一点。",
            "I'm sorry, I'll be gentler.",
            "ごめんね、優しくするよ。",
            "미안해, 살살 할게.");

        Add("還痛嗎？我先停一下。",
            "还痛吗？我先停一下。",
            "Does it still hurt? I'll pause for a moment.",
            "まだ痛い？ちょっと止まるね。",
            "아직 아파? 잠깐 멈출게.");

        Add("別忍著，告訴我哪裡不舒服。",
            "别忍着，告诉我哪里不舒服。",
            "Don't hold back, tell me where it hurts.",
            "我慢しないで、どこが痛いか言って。",
            "참지 말고, 어디가 아픈지 말해줘.");

        Add("我放慢一點，好嗎？",
            "我放慢一点，好吗？",
            "I'll slow down, alright?",
            "ペース落とすよ、いい？",
            "속도 줄일게, 괜찮지?");

        Add("才、才沒有要到呢～…哈啊…",
            "才、才没有要到呢～…哈啊…",
            "I-I'm not gonna cum yet~… Haah…",
            "い、逝くわけないでしょぉ〜…はぁっ…",
            "가, 갈 리가 없잖아~… 하아…");

        Add("不、不可以～…再這樣的話…",
            "不、不可以～…再这样的话…",
            "N-No… If you keep doing that…",
            "だ、ダメぇ〜…これ以上されたらぁ…",
            "아, 안 돼~… 이 이상 하면…");

        Add("雜魚…你、你給我停下來～…",
            "杂鱼…你、你给我停下来～…",
            "Loser… S-Stop it right now~…",
            "ざこ兄ちゃん…と、止まりなさいよぉ〜…",
            "허접 오빠… 머, 멈추란 말이야~…");

        Add("哼…才不會輸給你…嗯啊…",
            "哼…才不会输给你…嗯啊…",
            "Hmph… I won't lose to you… Nnh…",
            "ふん…負けるもんかぁ…んぁっ…",
            "흥… 오빠한테 질까 보냐… 으응…");

        Add("要、要壞掉了啦～…笨蛋…",
            "要、要坏掉了啦～…笨蛋…",
            "I'm gonna break… you dummy…",
            "こ、壊れちゃうよぉ……ばぁかぁ…❤",
            "망, 망가져버릴 것 같아… 바보야…❤");

        Add("不行…那裡…不要再弄了～",
            "不行…那里…不要再弄了～",
            "No… Not there… Don't touch there anymore~",
            "だめぇ…そこぉ…もういじっちゃダメぇ〜！",
            "안 돼… 거기… 더 이상 건드리지 마~!");

        Add("我、我才沒有…啊…",
            "我、我才没有…啊…",
            "I-I'm not… Ah…",
            "わ、私なんかが…あっ…",
            "내, 내가 그럴 리… 아앗…");

        Add("別、別看我的臉啦～…",
            "别、别看我的脸啦～…",
            "D-Don't look at my face~…",
            "か、顔見ないでよぉ〜っ…",
            "어, 얼굴 보지 말라니까~…");

        Add("啊…不行…要、要到了…",
            "啊…不行…要、要到了…",
            "Ah… I can't… I-I'm gonna cum…",
            "あっ…だめぇ…い、逝っちゃう…",
            "아앗… 안 돼… 가, 갈 것 같아…");

        Add("嗯…哥哥…快、快要…",
            "嗯…哥哥…快、快要…",
            "Nnh… big brother… I'm almost…",
            "んん…お兄ちゃん…も、もうすぐ…",
            "으응… 오빠… 거, 거의…");

        Add("等、等一下…有什麼要來了…",
            "等、等一下…有什么要来了…",
            "W-Wait… something's coming…",
            "ま、待ってぇ…なんか来ちゃう…",
            "기, 기다려… 뭔가가 오고 있어…");

        Add("呼…呼…不要停…",
            "呼…呼…不要停…",
            "Haah… haah… don't stop…",
            "はぁ…はぁ…止めないでぇ…",
            "하아… 하아… 멈추지 마…");

        Add("啊…我、我快不行了…",
            "啊…我、我快不行了…",
            "Ah… I-I can't hold it…",
            "あっ…わ、私もうダメぇ…",
            "아앗… 나, 나 더는 못 참겠어…");

        Add("嗯嗯…腦袋…一片空白…",
            "嗯嗯…脑袋…一片空白…",
            "Nnh… my head is… completely blank…",
            "んんっ…頭が…真っ白になっちゃう…",
            "으응… 머릿속이… 새하얘져…");

        Add("哥哥…抱緊我…",
            "哥哥…抱紧我…",
            "Big brother… hold me tight…",
            "お兄ちゃん…強く抱きしめて…",
            "오빠… 꽉 안아줘…");

        Add("啊…好奇怪…快要…",
            "啊…好奇怪…快要…",
            "Ah… so weird… I'm about to…",
            "あっ…変だよぉ…もうすぐ…",
            "아앗… 기분이 이상해… 거의 다…");

        Add("嘴硬什麼，都這樣了。",
            "嘴硬什么，都这样了。",
            "Still talking big? Look at you.",
            "強がり言っても、もう身体は正直だろ。",
            "허세 부리기는, 이미 온몸이 솔직한 주제에.");

        Add("是是是，你才沒有。",
            "是是是，你才没有。",
            "Sure, sure, you totally aren't.",
            "はいはい、そう言うことにしとくな。",
            "네네, 그렇다고 쳐줄게.");

        Add("想到就到吧，別忍。",
            "想到就到吧，别忍。",
            "Just let it out, don't hold back.",
            "我慢しなくていい、イク時はイケ。",
            "참지 마, 가고 싶을 땐 그냥 가버려.");

        Add("我看你還能撐多久。",
            "我看你还能撑多久。",
            "Let's see how much longer you can last.",
            "いつまで保つか見ものだな。",
            "얼마나 더 버티나 두고 보자.");

        Add("我在這裡，別怕。",
            "我在这里，别怕。",
            "I'm right here, don't be scared.",
            "俺はここにいる、怖がらなくていい。",
            "나 여기 있으니까 걱정 마.");

        Add("沒事的，放鬆就好。",
            "没事的，放松就好。",
            "It's okay, just relax.",
            "大丈夫だ、力を抜いて。",
            "괜찮아, 힘 빼면 돼.");

        Add("嗯，我抱著你。",
            "嗯，我抱着你。",
            "Yeah, I'm holding you.",
            "うん、ずっと抱きしめてるよ。",
            "응, 꼭 안고 있어.");

        Add("不用忍，交給我。",
            "不用忍，交给我。",
            "Don't hold back, leave it to me.",
            "我慢しなくていい、俺に任せろ。",
            "참지 않아도 돼, 나한테 맡겨.");

        Add("喂…你是不是快要不行了～？",
            "喂…你是不是快要不行了～？",
            "Hey… Are you already at your limit~?",
            "ねぇ…もしかしてもう限界なのぉ〜？❤",
            "저기… 벌써 한계 온 거야~?❤");

        Add("哼～這麼快就想投降了嗎～弱雞～",
            "哼～这么快就想投降了吗～弱鸡～",
            "Giving up this fast~? What a weakling~",
            "ふふっ、もうギブアップしちゃうのぉ〜？よわよわ❤",
            "후후, 벌써 항복하려고~? 약골 오빠❤");

        Add("雜魚的表情好奇怪喔～要射了嗎～？",
            "杂鱼的表情好奇怪喔～要射了吗～？",
            "Your face looks so funny, loser~ Gonna squirt~?",
            "ざこ兄ちゃん変な顔してる〜❤射精しちゃうのぉ〜？",
            "허접 오빠 표정 왜 그래~? 벌써 싸버리려고~?❤");

        Add("笨蛋…別在裡面…才怪～",
            "笨蛋…别在里面…才怪～",
            "Dummy… don't do it inside~ As if~",
            "ばぁか…中に出しちゃダメぇ〜…なーんてね❤",
            "바보야… 안에다 싸면 안 돼~… 농담이지만❤");

        Add("撐不住了吧～我都看出來了～",
            "撑不住了吧～我都看出来了～",
            "Can't hold it anymore~ I can tell~",
            "もう我慢できないんでしょ〜バレバレよぉ〜❤",
            "더는 못 참겠지~? 다 티 나거든~❤");

        Add("忍、忍一下啦～真是的～",
            "忍、忍一下啦～真是的～",
            "H-Hold it in a bit longer~ Geez~",
            "が、我慢しなさいよぉ〜まったくぅ❤",
            "조, 조금만 더 버텨보라고~ 정말이지❤");

        Add("哥哥…你是不是快要…？",
            "哥哥…你是不是快要…？",
            "Big brother… are you about to…?",
            "お兄ちゃん…もしかして出そう…？",
            "오빠… 혹시 쌀 것 같아…?");

        Add("嗯…感覺到了…你在發抖…",
            "嗯…感觉到了…你在发抖…",
            "Mm… I feel it… you're trembling…",
            "うん…伝わってくる…震えてるよ…",
            "응… 느껴져… 오빠 떨고 있어…");

        Add("沒關係…想射的話…可以喔。",
            "没关系…想射的话…可以喔。",
            "It's fine… if you want to cum… you can.",
            "大丈夫だよ…出したいなら…いいよ。",
            "괜찮아… 싸고 싶으면… 싸도 돼.");

        Add("……我、我會接住的。",
            "……我、我会接住的。",
            "……I-I'll catch it all.",
            "……わ、私が全部受け止めてあげる。",
            "……내, 내가 전부 받아줄게.");

        Add("呼…要、要來了嗎…？",
            "呼…要、要来了吗…？",
            "Haah… is it… coming…?",
            "はぁ…出、出ちゃうの…？",
            "하아… 나, 나오는 거야…?");

        Add("哥哥…不用忍耐也沒關係…",
            "哥哥…不用忍耐也没关系…",
            "Big brother… you don't have to hold back…",
            "お兄ちゃん…我慢しなくてもいいよ…",
            "오빠… 억지로 참지 않아도 괜찮아…");

        Add("嗯…我也…一起…",
            "嗯…我也…一起…",
            "Mm… me too… together…",
            "ん…私も…一緒に……",
            "응… 나도… 함께……");

        Add("哈啊……❤別停下來…雜魚❤",
            "哈啊……❤别停下来…杂鱼❤",
            "Haah……❤ Don't stop… loser❤",
            "はぁっ……❤止めないでよぉ…ざぁ〜こ❤",
            "하아……❤ 멈추지 마… 허접 오빠❤");

        Add("快點滿足我❤",
            "快点满足我❤",
            "Hurry and satisfy me❤",
            "早く満足させてぇ❤",
            "빨리 날 만족시켜줘❤");

        Add("快點…我想要……給我❤",
            "快点…我想要……给我❤",
            "Hurry… I want it… give it to me❤",
            "早くぅ…欲しいの……ちょうだい❤",
            "빨리… 원해…… 나한테 줘❤");

        Add("哈啊……❤…雜魚❤…好舒服❤",
            "哈啊……❤…杂鱼❤…好舒服❤",
            "Haah……❤… loser❤… feels so good❤",
            "はぁっ……❤…ざぁ〜こ❤…すっごく気持ちいいぃ❤",
            "하아……❤… 허접 오빠❤… 너무 기분 좋아❤");

        Add("啊啊啊……要來了……❤",
            "啊啊啊……要来了……❤",
            "Aaaah…… it's coming……❤",
            "あああっ……来ちゃう……❤",
            "아아악…… 나와버려……❤");

        Add("啊、啊啊…要、要來了…❤",
            "啊、啊啊…要、要来了…❤",
            "Ah, aah… it's, it's coming…❤",
            "あっ、ああぁっ…出、出ちゃうぅ…❤",
            "아, 아앗… 나, 나와버릴 것 같아…❤");

        Add("不要…不行…",
            "不要…不行…",
            "No… don't…",
            "いや…ダメぇ…",
            "싫어… 안 돼…");

        Add("不要、不可以…快停下…",
            "不要、不可以…快停下…",
            "No, stop… please stop…",
            "やだ、ダメぇ…早く止めてぇ…",
            "싫어, 안 돼… 제발 멈춰…");

        Add("不行不行…不要…",
            "不行不行…不要…",
            "No no… please don't…",
            "ダメダメぇ…やめてぇ…",
            "안 돼 안 돼… 하지 마…");

        Add("……嗯，謝謝。",
            "……嗯，谢谢。",
            "……Mm, thank you.",
            "……うん、ありがとう。",
            "……응, 고마워.");

        Add("呼…好多了。",
            "呼…好多了。",
            "Phew… that's much better.",
            "ふぅ…だいぶ楽になった。",
            "후우… 훨씬 낫다.");

        Add("……算你還有點體貼。",
            "……算你还有点体贴。",
            "……Guess you can be considerate after all.",
            "……少しは見直したわよ。",
            "……조금은 다정한 구석이 있네.");

        Add("嗯…就這樣…慢慢來。",
            "嗯…就这样…慢慢来。",
            "Mm… just like this… nice and slow.",
            "ん…このまま…ゆっくりね。",
            "응… 이대로… 천천히.");

        Add("啊…不、不要停…♡",
            "啊…不、不要停…♡",
            "Ah… d-don't stop…♡",
            "あっ…や、止めないでぇ…♡",
            "아앗… 머, 멈추지 마…♡");

        Add("嗯…就、就這樣…",
            "嗯…就、就这样…",
            "Mm… j-just like this…",
            "ん…そ、そのままぁ…",
            "응… 그, 그대로…");

        Add("哈啊…♡",
            "哈啊…♡",
            "Haah…♡",
            "はぁっ…♡",
            "하아…♡");

        Add("……笨蛋…別停下來…",
            "……笨蛋…别停下来…",
            "……Dummy… don't you dare stop…",
            "……ばぁか…止めないでよぉ…",
            "……바보야… 멈추지 말라고…");

        Add("已經硬了，這可都是莉奈的錯。",
            "已经硬了，这可都是莉奈的错。",
            "I'm already hard, and it's all your fault, Rina.",
            "もうガチガチだ、莉奈のせいだからな。",
            "벌써 단단해졌어, 다 리나 때문이야.");

        Add("準備好了……這根雜魚肉棒，已經等不及了",
            "准备好了……这根杂鱼肉棒，已经等不及了",
            "Ready now… This loser cock just can't wait.",
            "準備できたぞ……このざこチ○ポ、もう我慢できない。",
            "준비됐어…… 이 허접 자지, 이제 더는 못 기다려.");

        Add("妳已經迫不及待了吧？看我的",
            "妳已经迫不及待了吧？看我的",
            "You can't wait either, can you? Watch this.",
            "お前も待ちきれないだろ？いくぞ。",
            "너도 더 이상 못 참겠지? 잘 봐.");

        Add("雜魚肉棒要進去了喔。",
            "杂鱼肉棒要进去了喔。",
            "The loser cock is going in now.",
            "ざこチ○ポが入っちゃうぞ〜。",
            "허접 자지 들어간다~.");

        Add("雜魚雞雞裝備要把雜魚小穴填滿了哦。",
            "杂鱼鸡鸡装备要把杂鱼小穴填满了哦。",
            "The loser dick is about to fill up your tight little brat hole.",
            "ざこチ○ポで生意気なマ○コをいっぱいにしてやるよ。",
            "허접 고추로 건방진 보지를 꽉꽉 채워줄게.");

        Add("喝啊！全部進去了……好緊！",
            "喝啊！全部进去了……好紧！",
            "Nngh! It's all the way in… so tight!",
            "くっ！全部入った……すっごく締め付けてくる！",
            "크윽! 끝까지 다 들어갔어…… 완전 꽉 조여!");

        Add("咕……莉奈的小穴真是太爽了！",
            "咕……莉奈的小穴真是太爽了！",
            "Nnh… Rina's tight hole feels so damn good!",
            "ぐっ……莉奈の中、最高に気持ちいい……！",
            "으윽…… 리나의 조임 진짜 최고야……!");

        Add("呼……哈啊…小穴…夾得真緊啊……",
            "呼……哈啊…小穴…夹得真紧啊……",
            "Haah… haah… your hole is squeezing me so tightly……",
            "ふぅ……はぁ…すごく締め付けてくるな……",
            "후우…… 하아… 안이 정말 꽉 무는구나……");

        Add("被雜魚這樣狠狠地插……感覺怎麼樣？",
            "被杂鱼这样狠狠地插……感觉怎么样？",
            "Being thrust so deeply by a 'loser'… how does it feel?",
            "ざこ兄ちゃんに突かれて……どんな気持ちだ？",
            "허접한테 이렇게 찔려대니까…… 기분이 어때?");

        Add("小穴裡面一直在一縮一縮的，莉奈也很爽吧？",
            "小穴里面一直在一缩一缩的，莉奈也很爽吧？",
            "Your insides are throbbing non-stop. You love it too, don't you Rina?",
            "中がひくひく痙攣してるぞ、莉奈も気持ちいいんだろ？",
            "안이 쉴 새 없이 조여오는데, 리나도 기분 최고지?");

        Add("呼……不行了，快到極限了……",
            "呼……不行了，快到极限了……",
            "Phew… I can't hold back much longer, I'm at my limit……",
            "ふぅ……もうだめだ、限界に近い……",
            "후우…… 안 되겠어, 한계에 다다랐어……");

        Add("莉奈……我要射了，…我準備射出來了！",
            "莉奈……我要射了，…我准备射出来了！",
            "Rina… I'm gonna cum… I'm about to let it all out!",
            "莉奈……出る、全部出すぞ……！",
            "리나…… 싼다, 전부 싸버릴 거야……!");

        Add("裡面太舒服了……感覺隨時都會射出來……",
            "里面太舒服了……感觉随时都会射出来……",
            "It feels so good inside… like I could cum any second……",
            "中が気持ちよすぎて……いつ出てもおかしくない……",
            "안이 너무 기분 좋아서…… 언제 싸도 이상하지 않아……");

        Add("莉奈的小穴太棒了……我快不行了……",
            "莉奈的小穴太棒了……我快不行了……",
            "Rina, your pussy feels incredible… I'm losing control……",
            "莉奈の中が最高すぎて……もう保たない……",
            "리나 안이 너무 엄청나서…… 이제 못 버티겠어……");

        Add("不行了…要射了！",
            "不行了…要射了！",
            "I can't… I'm gonna cum!",
            "ダメだ…もう出るっ！",
            "안 돼… 싸버릴 것 같아!");

        Add("忍、忍不住了…要射了！",
            "忍、忍不住了…要射了！",
            "I-I can't hold it back… cumming!",
            "が、我慢できない…出ちゃう！",
            "모, 못 참겠어… 나온다!");

        Add("要、要射了…怎麼辦…！",
            "要、要射了…怎么办…！",
            "I-I'm gonna cum… what should I do…!",
            "で、出そうなんだ…どうすればいい…！",
            "싸, 쌀 것 같은데… 어쩌지…!");

        Add("射了……！啊啊啊！",
            "射了……！啊啊啊！",
            "Cumming……! Aaaah!",
            "出るっ……！あああっ！",
            "나온다……! 아아악!");

        Add("咕啊！收下吧！全部…的精液…全部射進莉奈的裡面了！",
            "咕啊！收下吧！全部…的精液…全部射进莉奈的里面了！",
            "Nngh! Take it! All of it… poured deep inside Rina!",
            "くっ！受け止めろ！全部…全部莉奈の中に注ぎ込んでやる！",
            "크윽! 받아내! 전부… 리나의 깊은 곳에 전부 쏟아부어줄게!");

        Add("射出來了……好爽…全都在妳的肚子裡！",
            "射出来了……好爽…全都在妳的肚子里！",
            "Poured it out…… feels incredible… all inside your belly!",
            "出た……最高だ…全部お前のお腹の中だ！",
            "다 쌌어…… 기분 최고다… 전부 네 뱃속에 들어갔어!");

        Add("射了！嗚啊啊！精液……全都灌進去了！",
            "射了！呜啊啊！精液……全都灌进去了！",
            "Shooting! Urrgh! Filled you all the way up!",
            "出たっ！うおおっ！全部…注ぎ尽くしてやる！",
            "싼다! 으아악! 전부… 가득 채워버렸어!");

        Add("…還沒完全硬起來，稍微等一下",
            "…还没完全硬起来，稍微等一下",
            "…Not fully hard yet, hold on a moment.",
            "…まだ立ちきってない、少し待ってくれ。",
            "…아직 덜 섰어, 조금만 기다려봐.");

        Add("不行，還沒準備好…",
            "不行，还没准备好…",
            "Not yet, not ready…",
            "ダメだ、まだ準備が…",
            "안 돼, 아직 준비가 덜 됐어…");

        Add("等等，還差一點…",
            "等等，还差一点…",
            "Wait, just a little more…",
            "待ってくれ、あと少し…",
            "잠깐만, 조금만 더…");

        Add("呼……差不多硬起來了。",
            "呼……差不多硬起来了。",
            "Phew… getting pretty hard now.",
            "ふぅ……だいぶ固くなってきたな。",
            "후우…… 슬슬 단단해졌네.");

        Add("感覺來了……已經變硬了。",
            "感觉来了……已经变硬了。",
            "Getting there… it's fully hard.",
            "いい感じだ……もうビンビンになってる。",
            "느낌 왔어…… 이미 발기했어.");

        Add("進去了……好緊。",
            "进去了……好紧。",
            "It's inside… so tight.",
            "入った……すごく締め付けられる。",
            "들어갔다…… 엄청 꽉 조여.");

        Add("呼……全都進去了。",
            "呼……全都进去了。",
            "Phew… it's all the way in.",
            "ふぅ……根元まで入ったよ。",
            "후우…… 끝까지 다 들어갔어.");

        Add("呼……呼……好舒服。",
            "呼……呼……好舒服。",
            "Haah… haah… feels so good.",
            "はぁ……はぁ……すごく気持ちいい。",
            "하아…… 하아…… 너무 기분 좋다.");

        Add("射了……！全都射在裡面了！",
            "射了……！全都射在里面了！",
            "I'm cumming…! Poured it all inside!",
            "出るっ……！中に全部出したぞ！",
            "나온다……! 안에 전부 싸버렸어!");

        Add("呼……射出來了。",
            "呼……射出来了。",
            "Phew… it's out.",
            "ふぅ……出たよ。",
            "후우…… 다 쌌어.");

        Add("……雜魚❤…再摸快一點❤",
            "……杂鱼❤…再摸快一点❤",
            "……Loser❤… touch me faster❤",
            "……ざぁ〜こ❤…もっと速く触ってぇ❤",
            "……허접 오빠❤… 좀 더 빨리 만져줘❤");

        Add("呼……好險，差點就出來了。",
            "呼……好险，差点就出来了。",
            "Phew…… that was close, almost came right there.",
            "ふぅ……危なかった、出ちゃうところだった。",
            "후우…… 위험했다, 하마터면 바로 나올 뻔했네.");

        Add("忍住了…稍微冷靜下來了…",
            "忍住了…稍微冷静下来了…",
            "Held it in… calmed down a bit…",
            "我慢できた…少し落ち着いたぞ…",
            "참아냈다… 조금 진정됐어…");

        Add("不行了……裡面太舒服了，沒辦法啊……",
            "不行了……里面太舒服了，没办法啊……",
            "I can't…… it feels too good inside, I can't hold it……",
            "ダメだ……中が気持ちよすぎて、無理だ……",
            "안 되겠어…… 안이 너무 기분 좋아서, 못 참겠어……");

        Add("啊啊啊出來了！",
            "啊啊啊出来了！",
            "Aaaah, coming out!",
            "ああああっ出ちゃう！",
            "아아악 나온다!");

        Add("可惡……完全忍不住了",
            "可恶……完全忍不住了",
            "Dammit…… I can't hold back at all",
            "くそっ……全然我慢できない",
            "젠장…… 도저히 못 참겠어");

        Add("忍不住，射了！",
            "忍不住，射了！",
            "Can't hold it, cumming!",
            "我慢できない、出る！",
            "못 참아, 싼다!");

        Add("沒辦法忍了！嗚啊啊啊！",
            "没办法忍了！呜啊啊啊！",
            "No way to hold it! Uaaargh!",
            "もう我慢できない！うああああっ！",
            "더는 못 참아! 으아아악!");

        Add("要、要去了～！！♡",
            "要、要去了～！！♡",
            "I-I'm gonna cum~!!♡",
            "い、いっちゃう～！！♡",
            "가、가버려～！！♡");

        Add("不、不行啊～！！",
            "不、不行啊～！！",
            "N-No, stop~!!",
            "ら、らめぇぇぇ～！！",
            "아、안 돼～！！");

        Add("腦袋一片空白……全部都要射出來了！",
            "脑袋一片空白……全部都要射出来了！",
            "My mind is blank…… shooting it all out!",
            "頭が真っ白だ……全部出ちゃう！",
            "머릿속이 하얘진다…… 전부 다 쏟아져 나와!");

          Add("啊…要進來了…！",
            "啊…要进来了…！",
            "Ah… it's coming in…!",
            "あっ…入ってくる…！",
            "아… 들어온다…!");

        Add("唔…那裡還…！",
            "唔…那里还…！",
            "Nnh… down there is still…!",
            "んっ…そこはまだ…！",
            "으… 거긴 아직…!");

    }
}
