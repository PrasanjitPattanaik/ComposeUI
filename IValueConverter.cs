using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

public class FileFolderIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            string iconPath = Directory.Exists(path) ? "pack://application:,,,/Icons/open-folder.png" :
                                                       "pack://application:,,,/Icons/new-file.png";
            return new BitmapImage(new Uri(iconPath));
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
