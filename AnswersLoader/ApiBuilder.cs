using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VkNet;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums.Filters;
using VkNet.Model;

namespace AnswersLoader
{
    public static class ApiBuilder
    {
        private static (string, string) GetLoginPass(IConfiguration config)
        {
            var login = config["login"];
            var pass = config["pass"];

            if (!string.IsNullOrEmpty(login) && !string.IsNullOrEmpty(login)) return (login, pass);
            
            Console.WriteLine("Введите логин:");
            login = Console.ReadLine();
            Console.WriteLine("Введите пароль:");
            pass = Console.ReadLine();

            return (login, pass);
        }
        
        public static VkApi GetApi(IConfiguration config)
        {
            var services = new ServiceCollection();
            services.AddAudioBypass();
            var api = new VkApi(services);

            var (login, pass) = GetLoginPass(config);

            try
            {
                api.Authorize(new ApiAuthParams
                {
                    ApplicationId = config.GetSection("app_id").Get<ulong>(),
                    Login = login,
                    Password = pass,
                    Settings = Settings.Messages | Settings.Documents,
                    TwoFactorAuthorization = () =>
                    {
                        Console.WriteLine("Введите код из sms:");
                        return Console.ReadLine();
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("При авторизации что-то отвалилось. Попробуйте ещё раз. Проверьте логин и пароль.");
                Console.WriteLine(ex.Message);
            }

            return api;
        }
    }
}