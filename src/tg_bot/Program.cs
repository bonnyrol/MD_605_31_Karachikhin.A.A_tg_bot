using dotenv.net;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace tg_bot
{
    internal class UserState
    {
        static public Dictionary<ChatId, UserState> userStates = new Dictionary<ChatId, UserState>();

        public bool? Choise { get; set; }
        public string? Message { get; set; }

        public UserState(ChatId chatId, bool? choise)
        {
            Choise = choise;
            userStates.Add(chatId, this);
        }

        public static UserState? GetUserState(ChatId chatId)
        {
            return userStates.ContainsKey(chatId) ? userStates[chatId] : null;
        }

        public static void UpdateUserState(ChatId chatId, bool? choise)
        {
            if (userStates.ContainsKey(chatId))
            {
                userStates[chatId].Choise = choise;
            }
            else
            {
                new UserState(chatId, choise);
            }
        }

        public static void ClearUserState(ChatId chatId)
        {
            if (userStates.ContainsKey(chatId))
            {
                userStates.Remove(chatId);
            }
        }
    }

    internal class Program
    {
        private static IDictionary<string, string> envVars;
        private static TelegramBotClient _bot;

        static async Task Main(string[] args)
        {
            DotEnv.Load();
            envVars = DotEnv.Read();
            var cts = new CancellationTokenSource();
            _bot = new TelegramBotClient(envVars["TG_TOKEN"], cancellationToken: cts.Token);

            var me = await _bot.GetMe();
            Console.WriteLine($"Бот запущен: @{me.Username}");

            _bot.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                cancellationToken: cts.Token
            );

            Console.WriteLine("Бот запущен. Нажмите Enter для выхода...");
            Console.ReadLine();

            cts.Cancel();
            await Task.CompletedTask;
        }

        private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type != UpdateType.Message || update.Message?.Type != MessageType.Text)
                return;

            var message = update.Message;
            var chatId = message.Chat.Id;

            Console.WriteLine($"Получено сообщение от {message.From?.Username}: {message.Text}");

            await ProcessMessage(message);
        }

        private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }

        private static async Task ProcessMessage(Message message)
        {
            bool? userChoice = null;
            var userState = UserState.GetUserState(message.Chat.Id);

            switch (message.Text)
            {
                case "/start":
                    await SendStartMessage(message);
                    break;

                case "/help":
                    await SendHelpMessage(message);
                    break;

                case "Закодировать в base64":
                    userChoice = true;
                    UserState.UpdateUserState(message.Chat.Id, userChoice);
                    await _bot.SendMessage(
                        message.Chat.Id,
                        "Отлично! Пришли мне любой текст, и я закодирую его в base64.\n" +
                        "Отправь /cancel для отмены операции.");
                    break;

                case "Декодировать base64":
                    userChoice = false;
                    UserState.UpdateUserState(message.Chat.Id, userChoice);
                    await _bot.SendMessage(
                        message.Chat.Id,
                        "Пришли мне текст в формате base64, и я расшифрую его.\n" +
                        "Отправь /cancel для отмены операции.");
                    break;

                case "/cancel":
                    UserState.ClearUserState(message.Chat.Id);
                    await _bot.SendMessage(
                        message.Chat.Id,
                        "Операция отменена. Выберите новое действие:",
                        replyMarkup: GetMainKeyboard());
                    break;

                default:
                    if (userState != null && userState.Choise.HasValue)
                    {
                        await ProcessUserInput(message, userState.Choise.Value);
                    }
                    else
                    {
                        await _bot.SendMessage(
                            message.Chat.Id,
                            "Пожалуйста, выберите действие с помощью меню ниже:",
                            replyMarkup: GetMainKeyboard());
                    }
                    break;
            }
        }

        private static async Task SendStartMessage(Message message)
        {
            await _bot.SendMessage(message.Chat.Id,
                "👋 Привет! Я бот для работы с base64.\n" +
                "Я умею кодировать текст в base64 и декодировать обратно.");

            await _bot.SendMessage(
                message.Chat.Id,
                "Выберите действие:",
                replyMarkup: GetMainKeyboard());
        }

        private static async Task SendHelpMessage(Message message)
        {
            await _bot.SendMessage(message.Chat.Id,
                "📖 **Помощь по использованию бота:**\n\n" +
                "• **Кодирование в base64** - преобразует обычный текст в base64\n" +
                "• **Декодирование base64** - преобразует base64 обратно в текст\n\n" +
                "**Команды:**\n" +
                "/start - перезапустить бота\n" +
                "/help - показать эту справку\n" +
                "/cancel - отменить текущую операцию\n\n" +
                "Просто выберите действие из меню и следуйте инструкциям!",
                parseMode: ParseMode.Markdown);
        }

        private static ReplyKeyboardMarkup GetMainKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    new KeyboardButton("Закодировать в base64"),
                    new KeyboardButton("Декодировать base64")
                },
                new[]
                {
                    new KeyboardButton("/help"),
                    new KeyboardButton("/start")
                }
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = false
            };
        }

        private static async Task ProcessUserInput(Message message, bool encode)
        {
            try
            {
                string result;

                if (encode)
                {
                    var plainTextBytes = Encoding.UTF8.GetBytes(message.Text);
                    result = Convert.ToBase64String(plainTextBytes);
                    
                    await _bot.SendMessage(
                        message.Chat.Id,
                        $"✅ **Текст закодирован в base64:**\n\n" +
                        $"`{result}`\n\n" +
                        $"Выберите следующее действие:",
                        parseMode: ParseMode.Markdown,
                        replyMarkup: GetMainKeyboard());
                }
                else
                {
                    try {

                        var base64EncodedBytes = Convert.FromBase64String(message.Text);
                        result = Encoding.UTF8.GetString(base64EncodedBytes);

                        await _bot.SendMessage(
                            message.Chat.Id,
                            $"✅ **Текст декодирован из base64:**\n\n" +
                            $"{result}\n\n" +
                            $"Выберите следующее действие:",
                            parseMode: ParseMode.Markdown,
                            replyMarkup: GetMainKeyboard());
                    }
                    catch (FormatException)
                    {
                        await _bot.SendMessage(
                            message.Chat.Id,
                            "❌ **Ошибка:** Это невалидный base64 текст.\n" +
                            "Пожалуйста, отправьте корректную строку base64 для декодирования.\n" +
                            "Или отправьте /cancel для отмены.");
                        return;
                    }
                }

                UserState.ClearUserState(message.Chat.Id);
            }
            catch (Exception ex)
            {
                await _bot.SendMessage(
                    message.Chat.Id,
                    $"❌ **Произошла ошибка:**\n{ex.Message}\n\n" +
                    $"Попробуйте еще раз или отправьте /cancel.");
                Console.WriteLine($"Ошибка при обработке сообщения: {ex}");
            }
        }
    }
}
