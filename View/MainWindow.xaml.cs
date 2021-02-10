using System.Windows;
using System.Windows.Input;

namespace AutoskipExample
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ((MainViewModel)DataContext).OnLoad();
        }

        #region VideoView
        private void videoview_MouseMove(object sender, MouseEventArgs e) { }
        private void videoview_DragLeave(object sender, DragEventArgs e) { }
        private void videoview_DragOver(object sender, DragEventArgs e) { }
        private void videoview_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effects = DragDropEffects.Copy;
        }
        private void videoview_Drop(object sender, DragEventArgs e)
        {
            string[] file = (string[])e.Data.GetData(DataFormats.FileDrop);

            ((MainViewModel)DataContext).videoviewDrop(file[0]);
        }
        #endregion
    }
}