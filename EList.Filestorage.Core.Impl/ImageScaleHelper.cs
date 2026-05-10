//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace EList.Filestorage.Core.Impl
{
    public static class ImageScaleHelper
    {
        /*
        public static Stream ResizeImageByPercent(Stream inputStream, int percent)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));

            if (percent <= 0)
                throw new ArgumentException("Процент должен быть больше 0", nameof(percent));

            // Сохраняем текущую позицию потока (если поддерживается)
            long originalPosition = 0;
            if (inputStream.CanSeek)
            {
                originalPosition = inputStream.Position;
                inputStream.Position = 0;
            }

            // Определяем формат исходного изображения
            ImageFormat originalFormat;
            using (var tempImage = Image.FromStream(inputStream, false, false))
            {
                originalFormat = tempImage.RawFormat;
            }

            // Если поток поддерживает позиционирование, восстанавливаем позицию
            if (inputStream.CanSeek)
                inputStream.Position = originalPosition;
            else
                inputStream.Position = 0; // Для потоков без Seek просто сбрасываем в начало

            // Загружаем исходное изображение
            using (var originalImage = Image.FromStream(inputStream))
            {
                // Вычисляем новые размеры
                int newWidth = (int)(originalImage.Width * percent / 100.0);
                int newHeight = (int)(originalImage.Height * percent / 100.0);

                // Создаём новое изображение с новыми размерами
                using (var resizedImage = new Bitmap(newWidth, newHeight))
                {
                    using (var graphics = Graphics.FromImage(resizedImage))
                    {
                        // Настройка высокого качества
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;

                        // Рисуем изменённое изображение
                        graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                    }

                    // Сохраняем в выходной поток
                    var outputStream = new MemoryStream();

                    // Сохраняем в исходном формате
                    resizedImage.Save(outputStream, originalFormat);
                    outputStream.Position = 0;

                    return outputStream;
                }
            }
        }
        */

        public static Stream ResizeImageByPercent(Stream inputStream, int percent)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));

            if (percent <= 0)
                throw new ArgumentException("Процент должен быть больше 0", nameof(percent));

            // Сбрасываем позицию потока в начало (если возможно)
            if (inputStream.CanSeek)
            {
                inputStream.Position = 0;
            }

            // Вариант 1: Простая загрузка (если поток в правильной позиции)
            try
            {
                using (var image = Image.Load(inputStream))
                {
                    int newWidth = (int)(image.Width * percent / 100.0);
                    int newHeight = (int)(image.Height * percent / 100.0);

                    image.Mutate(x => x.Resize(newWidth, newHeight));

                    var outputStream = new MemoryStream();

                    // Сохраняем в исходном формате
                    var encoder = image.Metadata.DecodedImageFormat;
                    image.Save(outputStream, encoder);
                    outputStream.Position = 0;

                    return outputStream;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка загрузки изображения: {ex.Message}", ex);
            }
        }


        //public static void ResizeImage(string sourcePath, string targetPath, int width, int height, ImageFormat format)
        //{
        //    using (var originalImage = new Bitmap(sourcePath))
        //    {
        //        // Создаем новый bitmap с новыми размерами
        //        using (var newImage = new Bitmap(width, height))
        //        {
        //            using (var graphics = Graphics.FromImage(newImage))
        //            {
        //                // Настройка качества интерполяции
        //                graphics.CompositingQuality = CompositingQuality.HighQuality;
        //                graphics.SmoothingMode = SmoothingMode.HighQuality;
        //                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

        //                // Отрисовываем исходное изображение в новом размере
        //                graphics.DrawImage(originalImage, 0, 0, width, height);

        //                // Сохраняем
        //                newImage.Save(targetPath);
        //            }
        //        }
        //    }
        //}
    }
}
