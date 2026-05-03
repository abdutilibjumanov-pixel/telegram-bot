using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.Linq;

long adminId = 8472744972;

var token = Environment.GetEnvironmentVariable("BOT_TOKEN");

if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("BOT_TOKEN topilmadi!");
    return;
}

var bot = new TelegramBotClient(token);

var allowedUsers = new long[] { 8546005296, 8311844956, 7237881666, 8472744972 };

Dictionary<long, List<string>> userPhotos = new();

Console.WriteLine("Bot ishga tushdi...");

bot.StartReceiving(
    async (botClient, update, token) =>
    {
        if (update.Message == null) return;

        var msg = update.Message;
        var chatId = msg.Chat.Id;

        Console.WriteLine("-----------");
        Console.WriteLine("ChatId: " + chatId);
        Console.WriteLine("Ism: " + msg.From?.FirstName);
        Console.WriteLine("Familiya: " + msg.From?.LastName);
        Console.WriteLine("Username: @" + msg.From?.Username);
        Console.WriteLine("UserId: " + msg.From?.Id);

        if (msg.Text != null)
            Console.WriteLine("Xabar: " + msg.Text);

        if (!allowedUsers.Contains(chatId))
        {
            await botClient.SendMessage(chatId, "❌ Sizga ruxsat yo‘q", cancellationToken: token);
            return;
        }

        if (msg.Text == "/start")
        {
            var keyboard = new ReplyKeyboardMarkup(new[]
            {
                KeyboardButton.WithRequestContact("📞 Kontakt yuborish")
            })
            {
                ResizeKeyboard = true
            };

            await botClient.SendMessage(
                chatId,
                "Salom!\n\n📞 Kontakt yuborsang ma’lumot chiqaraman.\n📸 PDF qilish uchun /pdfstart yoz.",
                replyMarkup: keyboard,
                cancellationToken: token
            );
            return;
        }

        if (msg.Contact != null)
        {
            var c = msg.Contact;

            string info =
                "📌 Kontakt ma’lumoti:\n\n" +
                $"👤 Ism: {c.FirstName}\n" +
                $"📱 Telefon: {c.PhoneNumber}\n" +
                $"🆔 User ID: {(c.UserId == null ? "yo‘q" : c.UserId.ToString())}";

            await botClient.SendMessage(chatId, info, cancellationToken: token);
            return;
        }

        if (msg.Text == "/pdfstart")
        {
            userPhotos[chatId] = new List<string>();

            await botClient.SendMessage(
                chatId,
                "📸 Rasmlarni yuboring.\nHammasini yuborib bo‘lgach /pdfmake yozing.",
                cancellationToken: token
            );
            return;
        }

        if (msg.Photo != null)
        {
            Console.WriteLine("Rasm yubordi. Rasm soni: " + msg.Photo.Length);

            if (!userPhotos.ContainsKey(chatId))
                userPhotos[chatId] = new List<string>();

            var bestPhoto = msg.Photo.Last();
            var file = await botClient.GetFile(bestPhoto.FileId, cancellationToken: token);

            Directory.CreateDirectory("photos");

            string imagePath = Path.Combine("photos", $"{chatId}_{DateTime.Now.Ticks}.jpg");

            using (var fs = new FileStream(imagePath, FileMode.Create))
            {
                await botClient.DownloadFile(file.FilePath!, fs, cancellationToken: token);
            }

            userPhotos[chatId].Add(imagePath);

            string userReport =
                "📥 Yangi rasm yuborildi\n\n" +
                $"👤 Ism: {msg.From?.FirstName}\n" +
                $"Familiya: {msg.From?.LastName ?? "yo‘q"}\n" +
                $"Username: {(string.IsNullOrEmpty(msg.From?.Username) ? "yo‘q" : "@" + msg.From.Username)}\n" +
                $"User ID: {msg.From?.Id}\n" +
                $"Chat ID: {chatId}\n" +
                $"Vaqt: {DateTime.Now}";

            await botClient.SendMessage(adminId, userReport, cancellationToken: token);

            await using (var adminStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
            {
                await botClient.SendPhoto(
                    adminId,
                    InputFile.FromStream(adminStream, "rasm.jpg"),
                    caption: "📸 Foydalanuvchi yuborgan rasm",
                    cancellationToken: token
                );
            }

            await botClient.SendMessage(
                chatId,
                $"✅ Rasm qabul qilindi. Jami: {userPhotos[chatId].Count} ta\nPDF qilish uchun /pdfmake yozing.",
                cancellationToken: token
            );

            return;
        }

        if (msg.Text == "/pdfmake")
        {
            if (!userPhotos.ContainsKey(chatId) || userPhotos[chatId].Count == 0)
            {
                await botClient.SendMessage(chatId, "❌ Avval rasm yuboring.", cancellationToken: token);
                return;
            }

            string pdfPath = $"photos_{chatId}.pdf";

            PdfDocument pdf = new PdfDocument();

            foreach (string imgPath in userPhotos[chatId])
            {
                PdfPage page = pdf.AddPage();
                XImage image = XImage.FromFile(imgPath);

                page.Width = image.PixelWidth;
                page.Height = image.PixelHeight;

                XGraphics gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(image, 0, 0, page.Width, page.Height);
            }

            pdf.Save(pdfPath);

            await using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);

            await botClient.SendDocument(
                chatId,
                InputFile.FromStream(stream, "rasmlar.pdf"),
                caption: "✅ Rasmlaringiz PDF qilindi.",
                cancellationToken: token
            );

            await botClient.SendMessage(
                adminId,
                $"📄 PDF tayyorlandi\nUser ID: {msg.From?.Id}\nChat ID: {chatId}\nVaqt: {DateTime.Now}",
                cancellationToken: token
            );

            userPhotos[chatId].Clear();

            await botClient.SendMessage(chatId, "Yangi PDF qilish uchun /pdfstart yozing.", cancellationToken: token);
            return;
        }

        await botClient.SendMessage(
            chatId,
            "Buyruqlar:\n/start\n/pdfstart\n/pdfmake",
            cancellationToken: token
        );
    },
    async (botClient, exception, token) =>
    {
        Console.WriteLine("Xato: " + exception.Message);
        await Task.CompletedTask;
    }
);

Console.ReadLine();
