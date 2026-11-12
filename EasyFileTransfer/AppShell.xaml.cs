namespace EasyFileTransfer
{
    /// <summary>
    /// アプリの画面遷移の土台になるShellクラスです。
    /// 今回はほぼ1画面だけですが、MAUIのテンプレートに合わせて使っています。
    /// </summary>
    public partial class AppShell : Shell
    {
        /// <summary>
        /// コンストラクタ：画面の中身を初期化します。
        /// </summary>
        public AppShell()
        {
            InitializeComponent();
        }
    }
}
