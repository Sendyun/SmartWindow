using System;
using System.Collections.Generic;

namespace SmartWindow
{
    internal enum UiLanguage
    {
        Chinese,
        English,
        Japanese,
        Korean,
        Russian,
        French
    }

    internal static class Localization
    {
        private static readonly Dictionary<string, string> Chinese =
            new Dictionary<string, string>
            {
                { "appName", "智慧窗" },
                { "consoleTitle", "智慧窗控制台" },
                { "controlCenter", "控制中心" },
                { "aboutAuthor", "关于作者" },
                { "runtimeStatus", "运行状态" },
                { "statusRunning", "通知服务运行中" },
                { "statusPaused", "通知接收已暂停" },
                { "statusRetry", "通知监听暂时中断，正在重试" },
                { "shortcuts", "快捷操作" },
                { "shortcutMusic", "音乐模式" },
                { "shortcutPersistent", "音乐窗常驻" },
                { "creator", "制作者" },
                { "controlSubtitle", "管理顶部通知、音乐识别与常驻显示" },
                { "notificationService", "通知接管" },
                { "notificationActive", "接收系统应用通知并显示在屏幕顶部" },
                { "notificationPaused", "当前已暂停，Windows 横幅仍会保持静默" },
                { "musicMode", "音乐模式" },
                { "musicOn", "正在监听 Windows 媒体会话与专辑信息" },
                { "musicOff", "识别正在播放的媒体并显示播放器胶囊" },
                { "musicPersistent", "音乐窗常驻" },
                { "persistentOn", "暂停播放后仍保留音乐信息与波形" },
                { "persistentOff", "暂停后显示片刻并自动收束" },
                { "autoStart", "开机自启动" },
                { "autoStartOn", "登录 Windows 后自动在后台启动智慧窗" },
                { "autoStartOff", "登录 Windows 后不会自动启动" },
                { "sizeTitle", "窗口大小" },
                { "sizeHint", "拖动滑块统一调整文字、图标和浮窗尺寸" },
                { "sizeSmall", "小" },
                { "sizeMedium", "中" },
                { "sizeLarge", "大" },
                { "transparentWindow", "透明智慧窗" },
                { "transparentOn", "使用透明玻璃效果，可透出桌面背景" },
                { "transparentOff", "使用不透明深色 HUD 背景" },
                { "qualityEnhancement", "质感增强" },
                { "qualityOn", "立体折射与动态光粒子已开启" },
                { "qualityOff", "增加立体光效、边缘折射和动态光粒子" },
                { "qualityRequiresGlass", "需要先开启透明智慧窗" },
                { "consoleTransparency", "控制台透明度" },
                { "consoleTransparencyHint", "拖动滑条调整控制台玻璃背景，0% 为纯黑" },
                { "testNotification", "发送测试通知" },
                { "notificationSettings", "系统通知设置" },
                { "exitApp", "退出智慧窗" },
                { "footerHint", "单击音乐窗展开播放控制，双击关闭；点击空白桌面自动收起。" },
                { "aboutSubtitle", "项目信息与反馈渠道" },
                { "projectDescription", "Windows 11 顶部通知与音乐控制浮窗" },
                { "author", "作者" },
                { "feedback", "Bug 反馈与功能建议" },
                { "emailHint", "点击邮箱地址可使用默认邮件应用发送反馈。" },
                { "openConsole", "打开应用控制台" },
                { "showWindow", "显示智慧窗" },
                { "demoNotification", "演示通知" },
                { "pauseReceive", "暂停接收" },
                { "resumeReceive", "继续接收" },
                { "exitRestore", "退出并恢复横幅" },
                { "startupTitle", "通知接管已就绪" },
                { "startupBody", "右下角横幅已静默，新的消息会从这里展开" },
                { "startupDemo", "演示模式：系统横幅仍保持开启" },
                { "demoTitle", "智慧窗演示" },
                { "demoBody", "圆角收起、展开、通知和音乐控制都已连接" },
                { "incoming", "// 新消息" },
                { "messageMode", "// 通知模式" },
                { "previous", "上一首" },
                { "pause", "暂停" },
                { "resume", "继续" },
                { "next", "下一首" },
                { "waitingMedia", "等待媒体播放" },
                { "musicWindowTitle", "音乐智慧窗" },
                { "nowPlaying", "正在播放" },
                { "media", "媒体" },
                { "newNotification", "新通知" },
                { "newNotificationBody", "你收到了一条新通知" },
                { "windows", "Windows" },
                { "appNotification", "应用通知" },
                { "language", "语言" }
            };

        private static readonly Dictionary<string, string> English =
            new Dictionary<string, string>
            {
                { "appName", "Smart Window" },
                { "consoleTitle", "Smart Window Console" },
                { "controlCenter", "Control Center" },
                { "aboutAuthor", "About the author" },
                { "runtimeStatus", "Runtime status" },
                { "statusRunning", "Notification service is running" },
                { "statusPaused", "Notifications are paused" },
                { "statusRetry", "Notification monitor interrupted; retrying" },
                { "shortcuts", "Shortcuts" },
                { "shortcutMusic", "Music mode" },
                { "shortcutPersistent", "Keep music window" },
                { "creator", "Created by" },
                { "controlSubtitle", "Manage top notifications, media detection and persistence" },
                { "notificationService", "Notification takeover" },
                { "notificationActive", "Show Windows app notifications at the top of the screen" },
                { "notificationPaused", "Notifications are paused; native banners remain hidden" },
                { "musicMode", "Music mode" },
                { "musicOn", "Listening to Windows media sessions and album data" },
                { "musicOff", "Detect playing media and show a player capsule" },
                { "musicPersistent", "Keep music window" },
                { "persistentOn", "Keep media information visible while playback is paused" },
                { "persistentOff", "Collapse automatically after playback pauses" },
                { "autoStart", "Start with Windows" },
                { "autoStartOn", "Start Smart Window in the background after sign-in" },
                { "autoStartOff", "Smart Window will not start automatically" },
                { "sizeTitle", "Window size" },
                { "sizeHint", "Adjust text, icons and overlay size together" },
                { "sizeSmall", "Small" },
                { "sizeMedium", "Medium" },
                { "sizeLarge", "Large" },
                { "transparentWindow", "Transparent window" },
                { "transparentOn", "Use translucent glass with the desktop visible" },
                { "transparentOff", "Use the opaque dark HUD surface" },
                { "qualityEnhancement", "Enhanced texture" },
                { "qualityOn", "Dimensional light, refraction and particles are active" },
                { "qualityOff", "Add dimensional light, edge refraction and moving particles" },
                { "qualityRequiresGlass", "Enable Transparent window first" },
                { "consoleTransparency", "Console transparency" },
                { "consoleTransparencyHint", "Adjust the console glass; 0% becomes solid black" },
                { "testNotification", "Send test notification" },
                { "notificationSettings", "Notification settings" },
                { "exitApp", "Exit Smart Window" },
                { "footerHint", "Click for playback controls, double-click to close. Clicking the desktop collapses it." },
                { "aboutSubtitle", "Project information and feedback" },
                { "projectDescription", "Windows 11 notification and media control overlay" },
                { "author", "Author" },
                { "feedback", "Bug reports and suggestions" },
                { "emailHint", "Click the address to send feedback with your default mail app." },
                { "openConsole", "Open console" },
                { "showWindow", "Show Smart Window" },
                { "demoNotification", "Demo notification" },
                { "pauseReceive", "Pause notifications" },
                { "resumeReceive", "Resume notifications" },
                { "exitRestore", "Exit and restore banners" },
                { "startupTitle", "Notification takeover is ready" },
                { "startupBody", "Native banners are muted; new messages appear here" },
                { "startupDemo", "Demo mode: native banners stay enabled" },
                { "demoTitle", "Smart Window demo" },
                { "demoBody", "Rounded transitions, notifications and music controls are ready" },
                { "incoming", "// INCOMING MESSAGE" },
                { "messageMode", "// MESSAGE MODE" },
                { "previous", "Previous" },
                { "pause", "Pause" },
                { "resume", "Resume" },
                { "next", "Next" },
                { "waitingMedia", "Waiting for media" },
                { "musicWindowTitle", "Music Smart Window" },
                { "nowPlaying", "Now playing" },
                { "media", "Media" },
                { "newNotification", "New notification" },
                { "newNotificationBody", "You have a new notification" },
                { "windows", "Windows" },
                { "appNotification", "App notification" },
                { "language", "Language" }
            };

        private static readonly Dictionary<string, string> Japanese =
            new Dictionary<string, string>
            {
                { "appName", "スマートウィンドウ" },
                { "consoleTitle", "スマートウィンドウ コンソール" },
                { "controlCenter", "コントロールセンター" },
                { "aboutAuthor", "作者について" },
                { "runtimeStatus", "実行状態" },
                { "statusRunning", "通知サービスは実行中です" },
                { "statusPaused", "通知は一時停止中です" },
                { "statusRetry", "通知監視を一時中断し、再試行しています" },
                { "shortcuts", "ショートカット" },
                { "shortcutMusic", "音楽モード" },
                { "shortcutPersistent", "音楽ウィンドウを常時表示" },
                { "creator", "制作" },
                { "controlSubtitle", "上部通知、音楽検出、常時表示を管理" },
                { "notificationService", "通知の引き継ぎ" },
                { "notificationActive", "Windows アプリの通知を画面上部に表示します" },
                { "notificationPaused", "通知は一時停止中です。標準バナーは非表示のままです" },
                { "musicMode", "音楽モード" },
                { "musicOn", "Windows のメディアセッションとアルバム情報を監視中" },
                { "musicOff", "再生中のメディアを検出してプレーヤーを表示します" },
                { "musicPersistent", "音楽ウィンドウを常時表示" },
                { "persistentOn", "一時停止してもメディア情報を表示します" },
                { "persistentOff", "一時停止後に自動で折りたたみます" },
                { "autoStart", "Windows 起動時に開始" },
                { "autoStartOn", "サインイン後にバックグラウンドで自動起動します" },
                { "autoStartOff", "Windows 起動時には自動起動しません" },
                { "sizeTitle", "ウィンドウサイズ" },
                { "sizeHint", "文字、アイコン、表示サイズをまとめて調整します" },
                { "sizeSmall", "小" },
                { "sizeMedium", "中" },
                { "sizeLarge", "大" },
                { "transparentWindow", "透明スマートウィンドウ" },
                { "transparentOn", "デスクトップが透けるガラス効果を使用" },
                { "transparentOff", "不透明なダーク HUD 背景を使用" },
                { "qualityEnhancement", "質感を強化" },
                { "qualityOn", "立体光、エッジ屈折、光粒子が有効です" },
                { "qualityOff", "立体光、エッジ屈折、動く光粒子を追加します" },
                { "qualityRequiresGlass", "先に透明スマートウィンドウを有効にしてください" },
                { "consoleTransparency", "コンソールの透明度" },
                { "consoleTransparencyHint", "ガラス背景を調整します。0% は黒になります" },
                { "testNotification", "テスト通知を送信" },
                { "notificationSettings", "通知設定" },
                { "exitApp", "スマートウィンドウを終了" },
                { "footerHint", "クリックで再生操作を展開、ダブルクリックで閉じます。" },
                { "aboutSubtitle", "プロジェクト情報とフィードバック" },
                { "projectDescription", "Windows 11 通知・メディア操作オーバーレイ" },
                { "author", "作者" },
                { "feedback", "不具合報告と提案" },
                { "emailHint", "メールアドレスをクリックすると標準メールアプリが開きます。" },
                { "openConsole", "コンソールを開く" },
                { "showWindow", "スマートウィンドウを表示" },
                { "demoNotification", "通知をデモ表示" },
                { "pauseReceive", "通知を一時停止" },
                { "resumeReceive", "通知を再開" },
                { "exitRestore", "終了してバナーを復元" },
                { "startupTitle", "通知の引き継ぎ準備完了" },
                { "startupBody", "標準バナーを停止しました。新しい通知はここに表示されます" },
                { "startupDemo", "デモモード：標準バナーは有効です" },
                { "demoTitle", "スマートウィンドウのデモ" },
                { "demoBody", "通知と音楽の操作アニメーションを確認できます" },
                { "incoming", "// 新しいメッセージ" },
                { "messageMode", "// 通知モード" },
                { "previous", "前へ" },
                { "pause", "一時停止" },
                { "resume", "再開" },
                { "next", "次へ" },
                { "waitingMedia", "メディアの再生を待機中" },
                { "musicWindowTitle", "音楽スマートウィンドウ" },
                { "nowPlaying", "再生中" },
                { "media", "メディア" },
                { "newNotification", "新しい通知" },
                { "newNotificationBody", "新しい通知があります" },
                { "windows", "Windows" },
                { "appNotification", "アプリ通知" },
                { "language", "言語" }
            };

        private static readonly Dictionary<string, string> Korean =
            new Dictionary<string, string>
            {
                { "appName", "스마트 윈도우" },
                { "consoleTitle", "스마트 윈도우 콘솔" },
                { "controlCenter", "제어 센터" },
                { "aboutAuthor", "제작자 정보" },
                { "runtimeStatus", "실행 상태" },
                { "statusRunning", "알림 서비스가 실행 중입니다" },
                { "statusPaused", "알림 수신이 일시 중지되었습니다" },
                { "statusRetry", "알림 감시가 중단되었습니다. 다시 시도 중" },
                { "shortcuts", "바로 가기" },
                { "shortcutMusic", "음악 모드" },
                { "shortcutPersistent", "음악 창 고정" },
                { "creator", "제작자" },
                { "controlSubtitle", "상단 알림, 음악 감지 및 고정 표시 관리" },
                { "notificationService", "알림 표시" },
                { "notificationActive", "Windows 앱 알림을 화면 상단에 표시합니다" },
                { "notificationPaused", "알림이 일시 중지되었습니다. 기본 배너는 숨겨집니다" },
                { "musicMode", "음악 모드" },
                { "musicOn", "Windows 미디어 세션과 앨범 정보를 감지 중" },
                { "musicOff", "재생 중인 미디어를 감지하여 플레이어를 표시합니다" },
                { "musicPersistent", "음악 창 고정" },
                { "persistentOn", "일시 중지 후에도 미디어 정보를 유지합니다" },
                { "persistentOff", "일시 중지 후 자동으로 접힙니다" },
                { "autoStart", "Windows 시작 시 실행" },
                { "autoStartOn", "로그인 후 백그라운드에서 자동으로 시작합니다" },
                { "autoStartOff", "Windows 시작 시 자동으로 실행하지 않습니다" },
                { "sizeTitle", "창 크기" },
                { "sizeHint", "글자, 아이콘 및 표시 크기를 함께 조정합니다" },
                { "sizeSmall", "작게" },
                { "sizeMedium", "보통" },
                { "sizeLarge", "크게" },
                { "transparentWindow", "투명 스마트 윈도우" },
                { "transparentOn", "바탕 화면이 보이는 반투명 유리 효과" },
                { "transparentOff", "불투명한 어두운 HUD 배경 사용" },
                { "qualityEnhancement", "질감 강화" },
                { "qualityOn", "입체 조명, 가장자리 굴절 및 광입자가 켜졌습니다" },
                { "qualityOff", "입체 조명, 가장자리 굴절 및 움직이는 광입자를 추가합니다" },
                { "qualityRequiresGlass", "먼저 투명 스마트 윈도우를 켜세요" },
                { "consoleTransparency", "콘솔 투명도" },
                { "consoleTransparencyHint", "콘솔 유리 배경을 조절하며 0%는 검은색입니다" },
                { "testNotification", "테스트 알림 보내기" },
                { "notificationSettings", "알림 설정" },
                { "exitApp", "스마트 윈도우 종료" },
                { "footerHint", "클릭하면 재생 컨트롤, 더블 클릭하면 닫힙니다." },
                { "aboutSubtitle", "프로젝트 정보 및 피드백" },
                { "projectDescription", "Windows 11 알림 및 미디어 제어 오버레이" },
                { "author", "제작자" },
                { "feedback", "버그 신고 및 제안" },
                { "emailHint", "이메일 주소를 클릭하면 기본 메일 앱이 열립니다." },
                { "openConsole", "콘솔 열기" },
                { "showWindow", "스마트 윈도우 표시" },
                { "demoNotification", "알림 데모" },
                { "pauseReceive", "알림 일시 중지" },
                { "resumeReceive", "알림 다시 시작" },
                { "exitRestore", "종료 및 배너 복원" },
                { "startupTitle", "알림 표시 준비 완료" },
                { "startupBody", "기본 배너가 숨겨졌습니다. 새 알림은 여기에 표시됩니다" },
                { "startupDemo", "데모 모드: 기본 배너가 활성화되어 있습니다" },
                { "demoTitle", "스마트 윈도우 데모" },
                { "demoBody", "알림과 음악 제어 애니메이션을 확인할 수 있습니다" },
                { "incoming", "// 새 메시지" },
                { "messageMode", "// 알림 모드" },
                { "previous", "이전" },
                { "pause", "일시 중지" },
                { "resume", "계속" },
                { "next", "다음" },
                { "waitingMedia", "미디어 재생 대기 중" },
                { "musicWindowTitle", "음악 스마트 윈도우" },
                { "nowPlaying", "재생 중" },
                { "media", "미디어" },
                { "newNotification", "새 알림" },
                { "newNotificationBody", "새 알림이 도착했습니다" },
                { "windows", "Windows" },
                { "appNotification", "앱 알림" },
                { "language", "언어" }
            };

        private static readonly Dictionary<string, string> Russian =
            new Dictionary<string, string>
            {
                { "appName", "Умное окно" },
                { "consoleTitle", "Консоль умного окна" },
                { "controlCenter", "Центр управления" },
                { "aboutAuthor", "Об авторе" },
                { "runtimeStatus", "Состояние" },
                { "statusRunning", "Служба уведомлений работает" },
                { "statusPaused", "Уведомления приостановлены" },
                { "statusRetry", "Служба уведомлений недоступна, повторная попытка" },
                { "shortcuts", "Горячие клавиши" },
                { "shortcutMusic", "Музыкальный режим" },
                { "shortcutPersistent", "Закрепить музыкальное окно" },
                { "creator", "Автор" },
                { "controlSubtitle", "Уведомления сверху, определение музыки и закрепление" },
                { "notificationService", "Перехват уведомлений" },
                { "notificationActive", "Показывать уведомления Windows в верхней части экрана" },
                { "notificationPaused", "Уведомления приостановлены; системные баннеры скрыты" },
                { "musicMode", "Музыкальный режим" },
                { "musicOn", "Отслеживание медиасеансов Windows и обложек" },
                { "musicOff", "Определять музыку и показывать плеер" },
                { "musicPersistent", "Закрепить музыкальное окно" },
                { "persistentOn", "Информация остаётся на экране после паузы" },
                { "persistentOff", "Автоматически сворачивать после паузы" },
                { "autoStart", "Запуск с Windows" },
                { "autoStartOn", "Автозапуск в фоне после входа в Windows" },
                { "autoStartOff", "Не запускать автоматически при входе" },
                { "sizeTitle", "Размер окна" },
                { "sizeHint", "Изменяет текст, значки и размер интерфейса" },
                { "sizeSmall", "Малый" },
                { "sizeMedium", "Средний" },
                { "sizeLarge", "Большой" },
                { "transparentWindow", "Прозрачное окно" },
                { "transparentOn", "Использовать полупрозрачное стекло" },
                { "transparentOff", "Использовать непрозрачный тёмный фон" },
                { "qualityEnhancement", "Улучшение текстуры" },
                { "qualityOn", "Объёмный свет, преломление и частицы включены" },
                { "qualityOff", "Добавить объёмный свет, преломление и световые частицы" },
                { "qualityRequiresGlass", "Сначала включите прозрачное окно" },
                { "consoleTransparency", "Прозрачность консоли" },
                { "consoleTransparencyHint", "Настройка стекла консоли; 0% — чёрный фон" },
                { "testNotification", "Тестовое уведомление" },
                { "notificationSettings", "Настройки уведомлений" },
                { "exitApp", "Выйти из умного окна" },
                { "footerHint", "Нажмите для управления, двойной щелчок закрывает." },
                { "aboutSubtitle", "Информация и обратная связь" },
                { "projectDescription", "Верхний интерфейс уведомлений и музыки Windows 11" },
                { "author", "Автор" },
                { "feedback", "Ошибки и предложения" },
                { "emailHint", "Нажмите на адрес, чтобы открыть почтовое приложение." },
                { "openConsole", "Открыть консоль" },
                { "showWindow", "Показать умное окно" },
                { "demoNotification", "Демонстрация" },
                { "pauseReceive", "Приостановить" },
                { "resumeReceive", "Продолжить" },
                { "exitRestore", "Выйти и восстановить баннеры" },
                { "startupTitle", "Перехват уведомлений готов" },
                { "startupBody", "Системные баннеры отключены; новые сообщения появятся здесь" },
                { "startupDemo", "Деморежим: системные баннеры включены" },
                { "demoTitle", "Демонстрация умного окна" },
                { "demoBody", "Анимации уведомлений и музыки готовы" },
                { "incoming", "// НОВОЕ СООБЩЕНИЕ" },
                { "messageMode", "// РЕЖИМ УВЕДОМЛЕНИЙ" },
                { "previous", "Назад" },
                { "pause", "Пауза" },
                { "resume", "Продолжить" },
                { "next", "Далее" },
                { "waitingMedia", "Ожидание медиа" },
                { "musicWindowTitle", "Музыкальное окно" },
                { "nowPlaying", "Сейчас играет" },
                { "media", "Медиа" },
                { "newNotification", "Новое уведомление" },
                { "newNotificationBody", "Получено новое уведомление" },
                { "windows", "Windows" },
                { "appNotification", "Уведомление приложения" },
                { "language", "Язык" }
            };

        private static readonly Dictionary<string, string> French =
            new Dictionary<string, string>
            {
                { "appName", "Fenêtre intelligente" },
                { "consoleTitle", "Console de la Fenêtre intelligente" },
                { "controlCenter", "Centre de contrôle" },
                { "aboutAuthor", "À propos de l’auteur" },
                { "runtimeStatus", "État du service" },
                { "statusRunning", "Le service de notifications est actif" },
                { "statusPaused", "Les notifications sont en pause" },
                { "statusRetry", "Surveillance interrompue, nouvelle tentative" },
                { "shortcuts", "Raccourcis" },
                { "shortcutMusic", "Mode musique" },
                { "shortcutPersistent", "Garder la fenêtre musique" },
                { "creator", "Créé par" },
                { "controlSubtitle", "Gérez les notifications, la musique et l’affichage persistant" },
                { "notificationService", "Prise en charge des notifications" },
                { "notificationActive", "Afficher les notifications Windows en haut de l’écran" },
                { "notificationPaused", "Notifications en pause ; les bannières natives restent masquées" },
                { "musicMode", "Mode musique" },
                { "musicOn", "Écoute des sessions multimédias et des pochettes Windows" },
                { "musicOff", "Détecter la musique et afficher un lecteur compact" },
                { "musicPersistent", "Garder la fenêtre musique" },
                { "persistentOn", "Conserver les informations pendant la pause" },
                { "persistentOff", "Se replier automatiquement après une pause" },
                { "autoStart", "Lancer avec Windows" },
                { "autoStartOn", "Lancer en arrière-plan après la connexion" },
                { "autoStartOff", "Ne pas lancer automatiquement" },
                { "sizeTitle", "Taille de la fenêtre" },
                { "sizeHint", "Ajuste ensemble le texte, les icônes et la taille" },
                { "sizeSmall", "Petite" },
                { "sizeMedium", "Moyenne" },
                { "sizeLarge", "Grande" },
                { "transparentWindow", "Fenêtre transparente" },
                { "transparentOn", "Utiliser un effet de verre translucide" },
                { "transparentOff", "Utiliser la surface HUD sombre opaque" },
                { "qualityEnhancement", "Texture améliorée" },
                { "qualityOn", "Lumière volumétrique, réfraction et particules actives" },
                { "qualityOff", "Ajouter lumière, réfraction des bords et particules" },
                { "qualityRequiresGlass", "Activez d’abord la fenêtre transparente" },
                { "consoleTransparency", "Transparence de la console" },
                { "consoleTransparencyHint", "Réglez le verre ; 0 % devient noir" },
                { "testNotification", "Envoyer une notification test" },
                { "notificationSettings", "Paramètres de notification" },
                { "exitApp", "Quitter Fenêtre intelligente" },
                { "footerHint", "Cliquez pour les commandes, double-cliquez pour fermer." },
                { "aboutSubtitle", "Informations du projet et commentaires" },
                { "projectDescription", "Superposition de notifications et de médias pour Windows 11" },
                { "author", "Auteur" },
                { "feedback", "Bogues et suggestions" },
                { "emailHint", "Cliquez sur l’adresse pour ouvrir votre application de messagerie." },
                { "openConsole", "Ouvrir la console" },
                { "showWindow", "Afficher la fenêtre" },
                { "demoNotification", "Notification de démonstration" },
                { "pauseReceive", "Suspendre" },
                { "resumeReceive", "Reprendre" },
                { "exitRestore", "Quitter et restaurer les bannières" },
                { "startupTitle", "La prise en charge est prête" },
                { "startupBody", "Les bannières natives sont masquées ; les messages apparaissent ici" },
                { "startupDemo", "Mode démo : les bannières natives restent actives" },
                { "demoTitle", "Démonstration" },
                { "demoBody", "Les animations et les contrôles multimédias sont prêts" },
                { "incoming", "// NOUVEAU MESSAGE" },
                { "messageMode", "// MODE NOTIFICATION" },
                { "previous", "Précédent" },
                { "pause", "Pause" },
                { "resume", "Reprendre" },
                { "next", "Suivant" },
                { "waitingMedia", "En attente du média" },
                { "musicWindowTitle", "Fenêtre musique" },
                { "nowPlaying", "Lecture en cours" },
                { "media", "Média" },
                { "newNotification", "Nouvelle notification" },
                { "newNotificationBody", "Vous avez reçu une nouvelle notification" },
                { "windows", "Windows" },
                { "appNotification", "Notification d’application" },
                { "language", "Langue" }
            };

        private static UiLanguage current = UiLanguage.Chinese;

        public static UiLanguage Current
        {
            get { return current; }
        }

        public static event EventHandler LanguageChanged;

        public static void SetLanguage(UiLanguage language)
        {
            if (current == language)
            {
                return;
            }
            current = language;
            EventHandler handler = LanguageChanged;
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        public static string Get(string key)
        {
            Dictionary<string, string> dictionary = DictionaryFor(current);
            string value;
            if (dictionary.TryGetValue(key, out value))
            {
                return value;
            }
            if (Chinese.TryGetValue(key, out value))
            {
                return value;
            }
            return key;
        }

        public static string Code(UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.English: return "en";
                case UiLanguage.Japanese: return "ja";
                case UiLanguage.Korean: return "ko";
                case UiLanguage.Russian: return "ru";
                case UiLanguage.French: return "fr";
                default: return "zh";
            }
        }

        public static UiLanguage FromCode(string code)
        {
            switch ((code ?? "").Trim().ToLowerInvariant())
            {
                case "en": return UiLanguage.English;
                case "ja": return UiLanguage.Japanese;
                case "ko": return UiLanguage.Korean;
                case "ru": return UiLanguage.Russian;
                case "fr": return UiLanguage.French;
                default: return UiLanguage.Chinese;
            }
        }

        public static string DisplayName(UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.English: return "English";
                case UiLanguage.Japanese: return "日本語";
                case UiLanguage.Korean: return "한국어";
                case UiLanguage.Russian: return "Русский";
                case UiLanguage.French: return "Français";
                default: return "中文";
            }
        }

        public static UiLanguage[] Languages
        {
            get
            {
                return new UiLanguage[]
                {
                    UiLanguage.Chinese,
                    UiLanguage.English,
                    UiLanguage.Japanese,
                    UiLanguage.Korean,
                    UiLanguage.Russian,
                    UiLanguage.French
                };
            }
        }

        public static string PreferredFontFamily
        {
            get
            {
                switch (current)
                {
                    case UiLanguage.Japanese: return "Yu Gothic UI";
                    case UiLanguage.Korean: return "Malgun Gothic";
                    case UiLanguage.English:
                    case UiLanguage.Russian:
                    case UiLanguage.French:
                        return "Segoe UI";
                    default:
                        return "汉仪文黑 85W";
                }
            }
        }

        private static Dictionary<string, string> DictionaryFor(
            UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.English: return English;
                case UiLanguage.Japanese: return Japanese;
                case UiLanguage.Korean: return Korean;
                case UiLanguage.Russian: return Russian;
                case UiLanguage.French: return French;
                default: return Chinese;
            }
        }
    }
}
