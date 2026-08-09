using DataGateMonitor.Models;
using DataGateMonitor.SharedModels.Enums;

namespace DataGateMonitor.DataBase.ConfigurationModels.Seeds;

public static class LocalizationTextSeedData
{
    public static LocalizationText[] GetData() => new[]
    {
        // Bot menu
        // /register - register to use the VPN\n
        // /register - εγγραφείτε για να χρησιμοποιήσετε το VPN\n
        // /register - зарегистрируйтесь для использования VPN\n
        new LocalizationText
        {
            Id = 1, Key = "BotMenu", Language = Language.English,
            Text =
                "<b><u>Bot Menu</u></b>:\n/get_my_files - get your files for connecting to the VPN" +
                "\n/make_new_file - create a new file for connecting to the VPN" +
                "\n/delete_selected_file - Delete a specific file" +
                "\n/delete_all_files - Delete all files" +
                "\n/how_to_use - receive information on how to use the VPN" +
                "\n/install_client - get a link to download the OpenVPN client for connecting to the VPN" +
                "\n/about_bot - receive information about this bot" +
                "\n/about_project - receive information about the project" +
                "\n/contacts - receive contacts developer" +
                "\n/change_language - Change your language/Изменить язык/Αλλάξτε τη γλώσσα σας"
        },
        new LocalizationText
        {
            Id = 2, Key = "BotMenu", Language = Language.Greek,
            Text =
                "<b><u>Μενού Bot</u></b>:\n/get_my_files - αποκτήστε τα αρχεία σας για σύνδεση στο VPN" +
                "\n/make_new_file - δημιουργήστε ένα νέο αρχείο για σύνδεση στο VPN" +
                "\n/delete_selected_file - Διαγραφή συγκεκριμένου αρχείου" +
                "\n/delete_all_files - Διαγραφή όλων των αρχείων" +
                "\n/how_to_use - λάβετε πληροφορίες για τη χρήση του VPN" +
                "\n/install_client - λάβετε σύνδεσμο για λήψη του OpenVPN client" +
                "\n/about_bot - λάβετε πληροφορίες για αυτό το bot" +
                "\n/about_project - λάβετε πληροφορίες για το έργο" +
                "\n/contacts - λάβετε στοιχεία επικοινωνίας του προγραμματιστή" +
                "\n/change_language - Change your language/Изменить язык/Αλλάξτε τη γλώσσα σας"
        },
        new LocalizationText
        {
            Id = 3, Key = "BotMenu", Language = Language.Russian,
            Text =
                "<b><u>Меню бота</u></b>:\n/get_my_files - получите свои файлы для подключения к VPN" +
                "\n/make_new_file - создайте новый файл для подключения к VPN" +
                "\n/delete_selected_file - Удалить выбранный файл" +
                "\n/delete_all_files - Удалить все файлы" +
                "\n/how_to_use - получите информацию о том, как использовать VPN" +
                "\n/install_client - получите ссылку для загрузки клиента OpenVPN" +
                "\n/about_bot - информация об этом боте" +
                "\n/about_project - информация о проекте" +
                "\n/contacts - контакты разработчика" +
                "\n/change_language - Change your language/Изменить язык/Αλλάξτε τη γλώσσα σας"
        },

        // About bot
        new LocalizationText
        {
            Id = 4, Key = "AboutBot", Language = Language.English,
            Text =
                "This bot is part of the DataGate application suite. It helps users manage their VPN connections easily. With this bot, you can:" +
                "\n- Get detailed instructions on how to use a VPN." +
                "\n- Register and obtain configuration files for VPN access." +
                "\n- Create new VPN configuration files if needed." +
                "\n- Download the OpenVPN client for seamless connection." +
                "\n- Learn about the bot's developer." +
                "\n\nThe bot is designed to provide quick and secure access to VPN features, ensuring user-friendly interaction and reliable support." +
                "\n\nFor more information about the application suite, please visit: https://datagateapp.com/"
        },
        new LocalizationText
        {
            Id = 5, Key = "AboutBot", Language = Language.Greek,
            Text =
                "Αυτό το bot είναι μέρος του συμπλέγματος εφαρμογών DataGate. Βοηθά τους χρήστες να διαχειρίζονται εύκολα τις συνδέσεις VPN τους. Με αυτό το bot, μπορείτε:" +
                "\n- Να λάβετε λεπτομερείς οδηγίες για τη χρήση VPN." +
                "\n- Να εγγραφείτε και να αποκτήσετε αρχεία διαμόρφωσης για πρόσβαση στο VPN." +
                "\n- Να δημιουργήσετε νέα αρχεία διαμόρφωσης VPN αν χρειάζεται." +
                "\n- Να κατεβάσετε τον OpenVPN client για ομαλή σύνδεση." +
                "\n- Να μάθετε για τον προγραμματιστή του bot." +
                "\n\nΤο bot είναι σχεδιασμένο για να παρέχει γρήγορη και ασφαλή πρόσβαση στις δυνατότητες του VPN, εξασφαλίζοντας φιλική προς το χρήστη αλληλεπίδραση και αξιόπιστη υποστήριξη." +
                "\n\nΓια περισσότερες πληροφορίες σχετικά με το συμπλέγμα εφαρμογών, επισκεφθείτε: https://datagateapp.com/"
        },
        new LocalizationText
        {
            Id = 6, Key = "AboutBot", Language = Language.Russian,
            Text =
                "Данный бот является частью комплекса приложений DataGate. Он помогает пользователям управлять подключениями VPN. С его помощью вы можете:" +
                "\n- Получить подробные инструкции по использованию VPN." +
                "\n- Зарегистрироваться и получить файлы конфигурации для доступа к VPN." +
                "\n- Создать новые файлы конфигурации VPN при необходимости." +
                "\n- Скачать клиент OpenVPN для удобного подключения." +
                "\n- Узнать о разработчике бота." +
                "\n\nБот создан для быстрого и безопасного доступа к возможностям VPN, обеспечивая удобное взаимодействие с пользователем и надёжную поддержку." +
                "\n\nПодробнее о комплексе приложений вы можете ознакомиться на сайте: https://datagateapp.com/"
        },

        // Successful registration
        new LocalizationText
        {
            Id = 7, Key = "Registered", Language = Language.English,
            Text = "You have successfully registered for VPN access!"
        },
        new LocalizationText
        {
            Id = 8, Key = "Registered", Language = Language.Greek,
            Text = "Έχετε εγγραφεί με επιτυχία για πρόσβαση στο VPN!"
        },
        new LocalizationText
        {
            Id = 9, Key = "Registered", Language = Language.Russian,
            Text = "Вы успешно зарегистрировались для доступа к VPN!"
        },

        // How to use VPN
        new LocalizationText
        {
            Id = 10, Key = "HowToUseVPN", Language = Language.English,
            Text =
                "To use the VPN, follow these steps:" +
                "\n1. Get Configuration Files:" +
                "\nUse the /get_my_files command to download your personal configuration files for OpenVPN." +
                "\n\n2. Install OpenVPN Client:" +
                "\nUse the /install_client command to get a link to download the official OpenVPN client." +
                "\nInstall the OpenVPN client on your device (Windows, macOS, Linux, or mobile)." +
                "\n\n3. Load Configuration Files:" +
                "\nOpen the OpenVPN client and import the configuration file you downloaded from the bot." +
                "\n\n4. Connect to VPN:" +
                "\nStart the OpenVPN client and select the imported configuration. Click 'Connect' to establish a secure connection." +
                "\n\n⚠️ **For users in Russia, Iran and other countries with stronger state control:**" +
                "\nPlease use the dedicated DataGate client. Configuration files from this bot and the standard OpenVPN client are not suitable for connection, as they may be blocked. You can download the DataGate client at: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 11, Key = "HowToUseVPN", Language = Language.Greek,
            Text =
                "Για να χρησιμοποιήσετε το VPN, ακολουθήστε αυτά τα βήματα:" +
                "\n1. Λήψη αρχείων διαμόρφωσης:" +
                "\nΧρησιμοποιήστε την εντολή /get_my_files για να κατεβάσετε τα προσωπικά σας αρχεία διαμόρφωσης για το OpenVPN." +
                "\n\n2. Εγκατάσταση OpenVPN Client:" +
                "\nΧρησιμοποιήστε την εντολή /install_client για να λάβετε σύνδεσμο για λήψη του επίσημου OpenVPN client." +
                "\nΕγκαταστήστε τον OpenVPN client στη συσκευή σας (Windows, macOS, Linux ή κινητό)." +
                "\n\n3. Φόρτωση αρχείων διαμόρφωσης:" +
                "\nΑνοίξτε τον OpenVPN client και εισαγάγετε το αρχείο διαμόρφωσης που κατεβάσατε από το bot." +
                "\n\n4. Σύνδεση με VPN:" +
                "\nΞεκινήστε τον OpenVPN client, επιλέξτε τη διαμόρφωση που εισαγάγατε και πατήστε 'Σύνδεση' για να δημιουργήσετε μια ασφαλή σύνδεση." +
                "\n\n⚠️ **Για χρήστες από Ρωσία, Ιράν και άλλες χώρες με ισχυρότερο κρατικό έλεγχο:**" +
                "\nΠαρακαλούμε χρησιμοποιήστε τον αφοσιωμένο πελάτη DataGate. Τα αρχεία διαμόρφωσης από αυτό το bot και ο τυπικός πελάτης OpenVPN δεν είναι κατάλληλοι για σύνδεση, καθώς μπορεί να αποκλειστούν. Μπορείτε να κατεβάσετε τον πελάτη DataGate στο: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 12, Key = "HowToUseVPN", Language = Language.Russian,
            Text =
                "Для использования VPN выполните следующие шаги:" +
                "\n1. Получение файлов конфигурации:" +
                "\nИспользуйте команду /get_my_files для загрузки ваших личных конфигурационных файлов для OpenVPN." +
                "\n\n2. Установка клиента OpenVPN:" +
                "\nИспользуйте команду /install_client, чтобы получить ссылку на загрузку официального клиента OpenVPN." +
                "\nУстановите клиент OpenVPN на ваше устройство (Windows, macOS, Linux или мобильное устройство)." +
                "\n\n3. Загрузка файлов конфигурации:" +
                "\nОткройте клиент OpenVPN и импортируйте файл конфигурации, который вы загрузили из бота." +
                "\n\n4. Подключение к VPN:" +
                "\nЗапустите клиент OpenVPN, выберите импортированную конфигурацию и нажмите 'Подключиться', чтобы установить безопасное соединение." +
                "\n\n⚠️ **Для пользователей из России, Ирана и других стран с более сильным государственным контролем:**" +
                "\nПожалуйста, воспользуйтесь собственным клиентом DataGate. Файлы из бота и стандартный клиент OpenVPN не подойдут для подключения, так как они блокируются. Скачать клиент можно по ссылке: https://datagateapp.com/download"
        },

        // Additional texts
        new LocalizationText
        {
            Id = 13, Key = "ChoosePlatform", Language = Language.English,
            Text =
                "Choose your platform to download the OpenVPN client or learn more about what OpenVPN is." +
                "\n\n⚠️ This option is for standard connection and is suitable for most countries. If you are from Russia, Iran or another country with higher state control over the internet (where VPN may be restricted), please use our DataGate application: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 14, Key = "ChoosePlatform", Language = Language.Greek,
            Text =
                "Επιλέξτε την πλατφόρμα σας για να κατεβάσετε τον OpenVPN client ή να μάθετε περισσότερα για το τι είναι το OpenVPN." +
                "\n\n⚠️ Αυτή η επιλογή είναι για τυπική σύνδεση και είναι κατάλληλη για τις περισσότερες χώρες. Αν είστε από Ρωσία, Ιράν ή άλλη χώρα με υψηλότερο κρατικό έλεγχο στο διαδίκτυο (όπου το VPN μπορεί να περιορίζεται), παρακαλούμε χρησιμοποιήστε την εφαρμογή DataGate: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 15, Key = "ChoosePlatform", Language = Language.Russian,
            Text =
                "Выберите свою платформу, чтобы скачать клиент OpenVPN или узнать больше о том, что такое OpenVPN." +
                "\n\n⚠️ Этот вариант предназначен для стандартного подключения и подходит для большинства стран. Если вы из России, Ирана или другой страны с более высоким государственным контролем за интернетом (где VPN может блокироваться), пожалуйста, воспользуйтесь нашим приложением DataGate: https://datagateapp.com/download"
        },

        new LocalizationText
        {
            Id = 16, Key = "ClientConfigCreated", Language = Language.English,
            Text = "Client configuration created successfully in UpdateHandler."
        },
        new LocalizationText
        {
            Id = 17, Key = "ClientConfigCreated", Language = Language.Greek,
            Text = "Η διαμόρφωση πελάτη δημιουργήθηκε με επιτυχία στο UpdateHandler."
        },
        new LocalizationText
        {
            Id = 18, Key = "ClientConfigCreated", Language = Language.Russian,
            Text = "Конфигурация клиента успешно создана в UpdateHandler."
        },

        new LocalizationText
        {
            Id = 19, Key = "HereIsConfig", Language = Language.English,
            Text = "Here is your OpenVPN configuration file."
        },
        new LocalizationText
        {
            Id = 20, Key = "HereIsConfig", Language = Language.Greek,
            Text = "Εδώ είναι το αρχείο διαμόρφωσης OpenVPN σας."
        },
        new LocalizationText
            { Id = 21, Key = "HereIsConfig", Language = Language.Russian, Text = "Вот ваш файл конфигурации OpenVPN." },

        new LocalizationText
        {
            Id = 22, Key = "DeveloperContacts", Language = Language.English,
            Text =
                "📞 **Developer Contacts** 📞" +
                "\n\nIf you have any questions, suggestions, or need assistance, feel free to contact me:" +
                "\n\n- **Project website**: https://datagateapp.com/" +
                "\n- **Telegram**: [Contact me](https://t.me/KolganovIvan)" +
                "\n- **Email**: imkolganov@gmail.com" +
                "\n- **GitHub**: [Profile](https://github.com/IMKolganov)" +
                "\n\nI am always happy to help and hear your feedback! 😊"
        },
        new LocalizationText
        {
            Id = 23, Key = "DeveloperContacts", Language = Language.Greek,
            Text =
                "📞 **Επαφές Προγραμματιστή** 📞" +
                "\n\nΑν έχετε οποιεσδήποτε ερωτήσεις, προτάσεις ή χρειάζεστε βοήθεια, μη διστάσετε να επικοινωνήσετε μαζί μου:" +
                "\n\n- **Ιστοσελίδα έργου**: https://datagateapp.com/" +
                "\n- **Telegram**: [Επικοινωνήστε μαζί μου](https://t.me/KolganovIvan)" +
                "\n- **Email**: imkolganov@gmail.com" +
                "\n- **GitHub**: [Προφίλ](https://github.com/IMKolganov)" +
                "\n\nΕίμαι πάντα χαρούμενος να βοηθήσω και να ακούσω τα σχόλιά σας! 😊"
        },
        new LocalizationText
        {
            Id = 24, Key = "DeveloperContacts", Language = Language.Russian,
            Text =
                "📞 **Контакты разработчика** 📞" +
                "\n\nЕсли у вас есть вопросы, предложения или нужна помощь, не стесняйтесь связаться со мной:" +
                "\n\n- **Сайт проекта**: https://datagateapp.com/" +
                "\n- **Telegram**: [Связаться со мной](https://t.me/KolganovIvan)" +
                "\n- **Email**: imkolganov@gmail.com" +
                "\n- **GitHub**: [Профиль](https://github.com/IMKolganov)" +
                "\n\nЯ всегда рад помочь и выслушать ваши отзывы! 😊"
        },

        new LocalizationText
        {
            Id = 25, Key = "AboutProject", Language = Language.English,
            Text =
                "🌐 **About this project** 🌐\n\nThis bot is part of the DataGate application suite. It provides VPN configuration files and access for connecting in regions where the internet is not heavily regulated." +
                "\n\nFor users in Russia, Iran and other countries with stronger state control over the internet, the standard OpenVPN client and configuration files from this bot may be blocked. For a stable and secure connection in such regions, please use the dedicated DataGate client." +
                "\n\nDownload the DataGate client and learn more about the application suite at: https://datagateapp.com/" +
                "\n\nProject website: https://datagateapp.com/"
        },
        new LocalizationText
        {
            Id = 26, Key = "AboutProject", Language = Language.Greek,
            Text =
                "🌐 **Σχετικά με αυτό το έργο** 🌐\n\nΑυτό το bot είναι μέρος του συμπλέγματος εφαρμογών DataGate. Παρέχει αρχεία διαμόρφωσης VPN και πρόσβαση για σύνδεση σε περιοχές όπου το διαδίκτυο δεν ρυθμίζεται αυστηρά." +
                "\n\nΓια χρήστες από Ρωσία, Ιράν και άλλες χώρες με ισχυρότερο κρατικό έλεγχο στο διαδίκτυο, ο τυπικός πελάτης OpenVPN και τα αρχεία διαμόρφωσης από αυτό το bot μπορεί να αποκλειστούν. Για σταθερή και ασφαλή σύνδεση σε τέτοιες περιοχές, παρακαλούμε χρησιμοποιήστε τον αφοσιωμένο πελάτη DataGate." +
                "\n\nΚατεβάστε τον πελάτη DataGate και μάθετε περισσότερα για το συμπλέγμα εφαρμογών στο: https://datagateapp.com/" +
                "\n\nΙστοσελίδα έργου: https://datagateapp.com/"
        },
        new LocalizationText
        {
            Id = 27, Key = "AboutProject", Language = Language.Russian,
            Text =
                "🌐 **О проекте** 🌐\n\nДанный бот является частью комплекса приложений DataGate. Он предоставляет файлы конфигурации VPN и доступ для подключения в странах, где интернет не подвержен жёсткому государственному регулированию." +
                "\n\nДля пользователей из России, Ирана и других стран с более сильным государственным контролем над интернетом стандартный клиент OpenVPN и файлы из бота могут блокироваться. Для стабильного и безопасного подключения в таких регионах воспользуйтесь собственным клиентом DataGate." +
                "\n\nСкачать клиент DataGate и подробнее ознакомиться с комплексом приложений можно на сайте: https://datagateapp.com/" +
                "\n\nСайт проекта: https://datagateapp.com/"
        },

        new LocalizationText
        {
            Id = 31, Key = "ChangeLanguage", Language = Language.English,
            Text = "/change_language - Change your language"
        },
        new LocalizationText
        {
            Id = 32, Key = "ChangeLanguage", Language = Language.Greek,
            Text = "/change_language - Αλλάξτε τη γλώσσα σας"
        },
        new LocalizationText
            { Id = 33, Key = "ChangeLanguage", Language = Language.Russian, Text = "/change_language - Изменить язык" },

        new LocalizationText
        {
            Id = 34, Key = "SuccessChangeLanguage", Language = Language.English,
            Text = "✅ You have successfully changed your language to English!"
        },
        new LocalizationText
        {
            Id = 35, Key = "SuccessChangeLanguage", Language = Language.Greek,
            Text = "✅ Έχετε αλλάξει τη γλώσσα σας σε Ελληνικά!"
        },
        new LocalizationText
        {
            Id = 36, Key = "SuccessChangeLanguage", Language = Language.Russian,
            Text = "✅ Вы успешно сменили язык на Русский!"
        },

        new LocalizationText
        {
            Id = 37, Key = "FilesNotFoundError", Language = Language.English,
            Text = "You have no files, but you can create them by selecting the /make_new_file command."
        },
        new LocalizationText
        {
            Id = 38, Key = "FilesNotFoundError", Language = Language.Russian,
            Text = "У вас нет файлов, но вы можете создать их, выбрав команду /make_new_file."
        },
        new LocalizationText
        {
            Id = 39, Key = "FilesNotFoundError", Language = Language.Greek,
            Text = "Δεν έχετε αρχεία, αλλά μπορείτε να τα δημιουργήσετε επιλέγοντας την εντολή /make_new_file."
        },

        new LocalizationText
        {
            Id = 40, Key = "MaxConfigError", Language = Language.English,
            Text = "Maximum limit of 10 configurations for your devices has been reached. Cannot create more files."
        },
        new LocalizationText
        {
            Id = 41, Key = "MaxConfigError", Language = Language.Russian,
            Text = "Достигнут максимальный лимит в 10 конфигураций для ваших устройств. Невозможно создать новые файлы."
        },
        new LocalizationText
        {
            Id = 42, Key = "MaxConfigError", Language = Language.Greek,
            Text =
                "Έχει επιτευχθεί το μέγιστο όριο 10 διαμορφώσεων για τις συσκευές σας. Δεν μπορείτε να δημιουργήσετε περισσότερα αρχεία."
        },

        new LocalizationText
        {
            Id = 43, Key = "SuccessfullyDeletedAllFile", Language = Language.English,
            Text = "All files have been successfully deleted."
        },
        new LocalizationText
        {
            Id = 44, Key = "SuccessfullyDeletedAllFile", Language = Language.Russian,
            Text = "Все файлы успешно удалены."
        },
        new LocalizationText
        {
            Id = 45, Key = "SuccessfullyDeletedAllFile", Language = Language.Greek,
            Text = "Όλα τα αρχεία διαγράφηκαν επιτυχώς."
        },

        new LocalizationText
        {
            Id = 46, Key = "ChooseFileForDelete", Language = Language.English, Text = "Please choose a file to delete."
        },
        new LocalizationText
        {
            Id = 47, Key = "ChooseFileForDelete", Language = Language.Russian,
            Text = "Пожалуйста, выберите файл для удаления."
        },
        new LocalizationText
        {
            Id = 48, Key = "ChooseFileForDelete", Language = Language.Greek,
            Text = "Παρακαλώ επιλέξτε ένα αρχείο για διαγραφή."
        },

        new LocalizationText
        {
            Id = 49, Key = "SuccessfullyDeletedFile", Language = Language.English,
            Text = "The selected file has been successfully deleted."
        },
        new LocalizationText
        {
            Id = 50, Key = "SuccessfullyDeletedFile", Language = Language.Russian,
            Text = "Выбранный файл был успешно удалён."
        },
        new LocalizationText
        {
            Id = 51, Key = "SuccessfullyDeletedFile", Language = Language.Greek,
            Text = "Το επιλεγμένο αρχείο διαγράφηκε επιτυχώς."
        },

        new LocalizationText { Id = 52, Key = "AboutOpenVPN", Language = Language.English, Text = "About OpenVPN" },
        new LocalizationText { Id = 53, Key = "AboutOpenVPN", Language = Language.Russian, Text = "О OpenVPN" },
        new LocalizationText
            { Id = 54, Key = "AboutOpenVPN", Language = Language.Greek, Text = "Σχετικά με το OpenVPN" },

        new LocalizationText
            { Id = 55, Key = "WhatIsRaspberryPi", Language = Language.English, Text = "What is Raspberry Pi?" },
        new LocalizationText
            { Id = 56, Key = "WhatIsRaspberryPi", Language = Language.Russian, Text = "Что такое Raspberry Pi?" },
        new LocalizationText
            { Id = 57, Key = "WhatIsRaspberryPi", Language = Language.Greek, Text = "Τι είναι το Raspberry Pi;" },

        new LocalizationText
        {
            Id = 58, Key = "CertCriticalError", Language = Language.English,
            Text =
                "Critical error. Something wrong with certification service. Now we stop all processing, please try again later."
        },
        new LocalizationText
        {
            Id = 59, Key = "CertCriticalError", Language = Language.Russian,
            Text =
                "Критическая ошибка. Что-то пошло не так в сервисе сертификации. Все операции остановлены, пожалуйста, попробуйте позже."
        },
        new LocalizationText
        {
            Id = 60, Key = "CertCriticalError", Language = Language.Greek,
            Text =
                "Κρίσιμο σφάλμα. Κάτι πήγε στραβά με την υπηρεσία πιστοποίησης. Τώρα σταματάμε όλες τις διαδικασίες, παρακαλώ δοκιμάστε αργότερα."
        },
        
        new LocalizationText
        {
            Id = 61, Key = "ChooseVpnServer", Language = Language.English,
            Text = "Choose an OpenVPN server:" +
                "\n\n⚠️ This option is for standard connection and is suitable for most countries. If you are from Russia, Iran or another country with higher state control over the internet (where VPN may be restricted), please use our DataGate application: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 62, Key = "ChooseVpnServer", Language = Language.Russian,
            Text = "Выберите сервер OpenVPN:" +
                "\n\n⚠️ **Для пользователей из России, Ирана и других стран с более сильным государственным контролем:**" +
                "\nПожалуйста, воспользуйтесь собственным клиентом DataGate. Файлы из бота и стандартный клиент OpenVPN не подойдут для подключения, так как они блокируются. Скачать клиент можно по ссылке: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 63, Key = "ChooseVpnServer", Language = Language.Greek,
            Text = "Επιλέξτε διακομιστή OpenVPN:" +
                "\n\n⚠️ Αυτή η επιλογή είναι για τυπική σύνδεση και είναι κατάλληλη για τις περισσότερες χώρες. Αν είστε από Ρωσία, Ιράν ή άλλη χώρα με υψηλότερο κρατικό έλεγχο στο διαδίκτυο (όπου το VPN μπορεί να περιορίζεται), παρακαλούμε χρησιμοποιήστε την εφαρμογή DataGate: https://datagateapp.com/download"
        },
        new LocalizationText
        {
            Id = 64, Key = "SomethingWentWrongWhenTryMakeNewFile", Language = Language.English,
            Text = "Something went wrong while trying to create a new file."
        },
        new LocalizationText
        {
            Id = 65, Key = "SomethingWentWrongWhenTryMakeNewFile", Language = Language.Russian,
            Text = "Произошла ошибка при попытке создать новый файл."
        },
        new LocalizationText
        {
            Id = 66, Key = "SomethingWentWrongWhenTryMakeNewFile", Language = Language.Greek,
            Text = "Κάτι πήγε στραβά κατά την προσπάθεια δημιουργίας νέου αρχείου."
        },
        new LocalizationText
        {
            Id = 67, Key = "ErrorDeletedAllFile", Language = Language.English,
            Text = "No files found to delete."
        },
        new LocalizationText
        {
            Id = 68, Key = "ErrorDeletedAllFile", Language = Language.Russian,
            Text = "Файлы для удаления не найдены."
        },
        new LocalizationText
        {
            Id = 69, Key = "ErrorDeletedAllFile", Language = Language.Greek,
            Text = "Δεν βρέθηκαν αρχεία προς διαγραφή."
        },
        new LocalizationText
        {
            Id = 70, Key = "ErrorDeletedFile", Language = Language.English,
            Text = "File not found or already deleted."
        },
        new LocalizationText
        {
            Id = 71, Key = "ErrorDeletedFile", Language = Language.Russian,
            Text = "Файл не найден или уже удалён."
        },
        new LocalizationText
        {
            Id = 72, Key = "ErrorDeletedFile", Language = Language.Greek,
            Text = "Το αρχείο δεν βρέθηκε ή έχει ήδη διαγραφεί."
        },

        new LocalizationText
        {
            Id = 73, Key = "DashboardLoginCode", Language = Language.English,
            Text =
                "Your dashboard login code:\n\n" +
                "<code>{code}</code>\n\n" +
                "Valid for {minutes} min. Enter it on the DataGate Monitor sign-in page under «Continue with Telegram».\n" +
                "Do not share this code."
        },
        new LocalizationText
        {
            Id = 74, Key = "DashboardLoginCode", Language = Language.Greek,
            Text =
                "Ο κωδικός σύνδεσης στον πίνακα:\n\n" +
                "<code>{code}</code>\n\n" +
                "Ισχύει για {minutes} λεπτά. Εισαγάγετέ τον στη σελίδα σύνδεσης DataGate Monitor στην επιλογή «Continue with Telegram».\n" +
                "Μην μοιράζεστε τον κωδικό."
        },
        new LocalizationText
        {
            Id = 75, Key = "DashboardLoginCode", Language = Language.Russian,
            Text =
                "Код для входа в панель:\n\n" +
                "<code>{code}</code>\n\n" +
                "Действует {minutes} мин. Введите его на странице входа DataGate Monitor в разделе «Continue with Telegram».\n" +
                "Не передавайте код никому."
        },

        new LocalizationText
        {
            Id = 76, Key = "DashboardLoginCodeError", Language = Language.English,
            Text =
                "Could not issue a login code. Register in the bot with /register first, or contact support if you are blocked."
        },
        new LocalizationText
        {
            Id = 77, Key = "DashboardLoginCodeError", Language = Language.Greek,
            Text =
                "Αδυναμία έκδοσης κωδικού. Κάντε πρώτα εγγραφή με /register ή επικοινωνήστε με την υποστήριξη αν έχετε αποκλειστεί."
        },
        new LocalizationText
        {
            Id = 78, Key = "DashboardLoginCodeError", Language = Language.Russian,
            Text =
                "Не удалось выдать код. Сначала зарегистрируйтесь в боте через /register или обратитесь в поддержку, если аккаунт заблокирован."
        },

        new LocalizationText
        {
            Id = 79, Key = "AccountLinkTelegramAlreadyLinkedToGoogle", Language = Language.English,
            Text =
                "This Telegram account is already linked to Google account {accountLabel}. Sign in with that Google account in the app."
        },
        new LocalizationText
        {
            Id = 80, Key = "AccountLinkTelegramAlreadyLinkedToGoogle", Language = Language.Greek,
            Text =
                "Αυτός ο λογαριασμός Telegram είναι ήδη συνδεδεμένος με τον Google λογαριασμό {accountLabel}. Συνδεθείτε στην εφαρμογή με αυτόν τον Google λογαριασμό."
        },
        new LocalizationText
        {
            Id = 81, Key = "AccountLinkTelegramAlreadyLinkedToGoogle", Language = Language.Russian,
            Text =
                "Этот Telegram уже привязан к Google-аккаунту {accountLabel}. Войдите в приложение под этим Google."
        },

        new LocalizationText
        {
            Id = 82, Key = "AccountLinkSuccess", Language = Language.English,
            Text = "Accounts linked successfully. User #{userId}"
        },
        new LocalizationText
        {
            Id = 83, Key = "AccountLinkSuccess", Language = Language.Greek,
            Text = "Οι λογαριασμοί συνδέθηκαν επιτυχώς. Χρήστης #{userId}"
        },
        new LocalizationText
        {
            Id = 84, Key = "AccountLinkSuccess", Language = Language.Russian,
            Text = "Аккаунты успешно связаны. Пользователь #{userId}"
        },

        new LocalizationText
        {
            Id = 85, Key = "AccountLinkAlreadyLinked", Language = Language.English,
            Text = "Accounts are already linked."
        },
        new LocalizationText
        {
            Id = 86, Key = "AccountLinkAlreadyLinked", Language = Language.Greek,
            Text = "Οι λογαριασμοί είναι ήδη συνδεδεμένοι."
        },
        new LocalizationText
        {
            Id = 87, Key = "AccountLinkAlreadyLinked", Language = Language.Russian,
            Text = "Аккаунты уже связаны."
        },

        new LocalizationText
        {
            Id = 88, Key = "AccountLinkEnterCodePrompt", Language = Language.English,
            Text = "Enter the code from the app:\n/link_account CODE\n\nOr send the 8-character code alone in this chat."
        },
        new LocalizationText
        {
            Id = 89, Key = "AccountLinkEnterCodePrompt", Language = Language.Greek,
            Text = "Εισαγάγετε τον κωδικό από την εφαρμογή:\n/link_account CODE\n\nΉ στείλτε μόνο τους 8 χαρακτήρες σε αυτή τη συνομιλία."
        },
        new LocalizationText
        {
            Id = 90, Key = "AccountLinkEnterCodePrompt", Language = Language.Russian,
            Text = "Введите код из приложения:\n/link_account КОД\n\nИли отправьте 8 символов кода отдельным сообщением в этот чат."
        },

        new LocalizationText
        {
            Id = 91, Key = "AccountLinkInvalidCodeFormat", Language = Language.English,
            Text = "Invalid code format. Expected 8 characters (A-Z, 2-9)."
        },
        new LocalizationText
        {
            Id = 92, Key = "AccountLinkInvalidCodeFormat", Language = Language.Greek,
            Text = "Μη έγκυρη μορφή κωδικού. Αναμένονται 8 χαρακτήρες (A-Z, 2-9)."
        },
        new LocalizationText
        {
            Id = 93, Key = "AccountLinkInvalidCodeFormat", Language = Language.Russian,
            Text = "Неверный формат кода. Нужны 8 символов (A-Z, 2-9)."
        },

        new LocalizationText
        {
            Id = 94, Key = "AccountLinkNotRegistered", Language = Language.English,
            Text = "Telegram account is not registered. Use /register in the bot first."
        },
        new LocalizationText
        {
            Id = 95, Key = "AccountLinkNotRegistered", Language = Language.Greek,
            Text = "Ο λογαριασμός Telegram δεν είναι εγγεγραμμένος. Χρησιμοποιήστε πρώτα /register στο bot."
        },
        new LocalizationText
        {
            Id = 96, Key = "AccountLinkNotRegistered", Language = Language.Russian,
            Text = "Telegram-аккаунт не зарегистрирован. Сначала используйте /register в боте."
        },

        new LocalizationText
        {
            Id = 97, Key = "AccountLinkFailed", Language = Language.English,
            Text = "Could not link accounts. Check the code and try again."
        },
        new LocalizationText
        {
            Id = 98, Key = "AccountLinkFailed", Language = Language.Greek,
            Text = "Αποτυχία σύνδεσης λογαριασμών. Ελέγξτε τον κωδικό και δοκιμάστε ξανά."
        },
        new LocalizationText
        {
            Id = 99, Key = "AccountLinkFailed", Language = Language.Russian,
            Text = "Не удалось связать аккаунты. Проверьте код и попробуйте снова."
        },

        new LocalizationText
        {
            Id = 100, Key = "VpnServerNotAllowedByQuotaPlan", Language = Language.English,
            Text =
                "This VPN server is not available on your current plan. Please choose another server or upgrade your plan."
        },
        new LocalizationText
        {
            Id = 101, Key = "VpnServerNotAllowedByQuotaPlan", Language = Language.Greek,
            Text =
                "Αυτός ο διακομιστής VPN δεν είναι διαθέσιμος στο τρέχον πρόγραμμά σας. Επιλέξτε άλλον διακομιστή ή αναβαθμίστε το πρόγραμμα."
        },
        new LocalizationText
        {
            Id = 102, Key = "VpnServerNotAllowedByQuotaPlan", Language = Language.Russian,
            Text =
                "Этот VPN-сервер недоступен на вашем текущем тарифе. Выберите другой сервер или обновите план."
        },
    };

}