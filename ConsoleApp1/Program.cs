using System;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Drawing;
using System.Drawing.Imaging;
using Telegram.Bot.Exceptions;

namespace Program {
    class Program
    {
        static string lastUserText = "";
        static int lastMessageId = 0;
        static async Task Main()
        {
            var client = new TelegramBotClient("6237960866:AAH1HxVOCN0vJwW6s22kl4Mv49QTpukwgM8");
            client.StartReceiving(Update, Error);
            Console.WriteLine("Хэлоу Ворлд!");
            Console.ReadLine();
        }

        async static Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            var message = update.Message;
            if (message.Text != null) 
            {
                lastUserText = message.Text;
                lastMessageId = message.MessageId;
                await botClient.SendTextMessageAsync(message.Chat.Id, $"Ваш текст сохранён: {lastUserText}");
                return;
            }
            if (message.Photo != null) 
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Крутое фото!");

                var photo = message.Photo[^1]; // Используем индексатор для доступа к последнему элементу
                var fileId = photo.FileId;
                var fileInfo = await botClient.GetFileAsync(fileId);
                var filePath = fileInfo.FilePath;

                if (filePath == null)
                {
                    Console.WriteLine("Ошибка: путь к файлу не найден(фото).");
                    return;
                }

                string destinationFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileId + ".jpg");

                /*string newFileName = "фото" + Path.GetExtension(message.Document.FileName);
                string destinationFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", newFileName);*/
                await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                await botClient.DownloadFileAsync(
                    filePath: filePath,
                    destination: fileStream,
                    cancellationToken: token);
                fileStream.Close();

                if (!string.IsNullOrEmpty(lastUserText))
                {
                    AddTextToImage(destinationFilePath, lastUserText);

                    destinationFilePath = Path.Combine(Path.GetDirectoryName(destinationFilePath),
                                       Path.GetFileNameWithoutExtension(destinationFilePath) + "_edited.jpg");
                }

                await using Stream stream = System.IO.File.OpenRead(destinationFilePath);
                Message sentMessage = await botClient.SendDocumentAsync(
                    chatId: message.Chat.Id,
                    document: InputFile.FromStream(stream: stream, fileId + "(edited).jpg"),
                    caption: "Был наложен текст на фото. Можете скачать новое фото:");

                /*await botClient.SendTextMessageAsync(message.Chat.Id, "Сообщение для проверки на отправку фотки");*/

                // Получение объекта отправленного сообщения и его идентификатора
                //Message sentMessage = await botClient.SendTextMessageAsync(message.Chat.Id, "Some text");
                int sentMessageId = sentMessage.MessageId;

                // Удаление предыдущего сообщения
                await ClearChat(botClient, message.Chat, lastMessageId + 1, sentMessageId - 1);


                return;
            }
            if (message.Document != null)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "Крутой документ, но я не смогу его обратотать");

                /*
                var fileId = update.Message.Document.FileId;
                var fileInfo = await botClient.GetFileAsync(fileId);
                var filePath = fileInfo.FilePath;
                if (filePath == null)
                {
                    Console.WriteLine("Ошибка: путь к файлу не найден(документ).");
                    return;
                }*/

                /*
                string destinationFilePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\{message.Document.FileName}";
                string newFileName = "фото" + Path.GetExtension(message.Document.FileName);
                string destinationFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", newFileName);
                await using Stream fileStream = System.IO.File.Create(destinationFilePath);
                await botClient.DownloadFileAsync(
                    filePath: filePath,
                    destination: fileStream,
                    cancellationToken: token);
                fileStream.Close();
                
                await using Stream stream = System.IO.File.OpenRead(destinationFilePath);
                await botClient.SendDocumentAsync(
                    chatId: message.Chat.Id,
                    document: InputFile.FromStream(stream: stream, message.Document.FileName.Replace("jpg", "(edited).jpg")),
                    caption: "Ваш документ:");

                /*await botClient.SendTextMessageAsync(message.Chat.Id, "Сообщение для проверки на отправку фотки");

                */
                return;
            }
        }

        private static Task Error(ITelegramBotClient client, Exception exception, CancellationToken token)
        {
            Console.WriteLine($"An error occurred: {exception.Message}");
            return Task.CompletedTask;
        }
        private static void AddTextToImage(string originalImagePath, string text)
        {
            try
            {
                using (var image = Image.FromFile(originalImagePath))
                {
                    using (var graphics = Graphics.FromImage(image))
                    {
                        using (var font = new Font("Georgia", 50)) // Увеличенный размер шрифта
                        {
                            var brush = Brushes.Black;
                            var format = new StringFormat()
                            {
                                Alignment = StringAlignment.Center, // Горизонтальное выравнивание по центру
                                LineAlignment = StringAlignment.Center // Вертикальное выравнивание по центру
                            };

                            // Рассчитывается положение для текста так, чтобы он был снизу.
                            var rect = new RectangleF(0, 300, image.Width, image.Height);

                            // Рисуется текст.
                            graphics.DrawString(text, font, brush, rect, format);
                        }
                    }

                    // Создание нового имя файла для измененного изображения.
                    string editedImagePath = Path.Combine(Path.GetDirectoryName(originalImagePath),
                                                          Path.GetFileNameWithoutExtension(originalImagePath) + "_edited.jpg");

                    // Сохранение измененного изображения под новым именем.
                    image.Save(editedImagePath, ImageFormat.Jpeg);
                }
            }
            catch (Exception ex)
            {
                // Вывод сообщения об ошибке в консоль.
                Console.WriteLine($"Ошибка при добавлении текста на изображение: {ex.Message}");
            }
        }

        private static async Task ClearChat(ITelegramBotClient botClient, Chat chat, int startMessageId, int endMessageId)
        {
            for (int messageId = startMessageId; messageId <= endMessageId; messageId++)
            {
                try
                {
                    await botClient.DeleteMessageAsync(chat.Id, messageId);
                }
                catch (ApiRequestException ex) when (ex.Message.Contains("message can't be deleted for everyone"))
                {
                    // Ignore the error and continue deleting other messages
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Не удалось удалить сообщение с ID {messageId}: {ex.Message}");
                }
            }
        }


    }
}
    
