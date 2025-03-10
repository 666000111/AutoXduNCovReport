﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoXduNCovReport.Repository;
using Cocona;

namespace AutoXduNCovReport
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await CoconaApp.RunAsync<Program>(args, options =>
            {
                options.EnableShellCompletionSupport = false;
                options.TreatPublicMethodsAsCommands = false;
            });
        }

        [Command("ncov", Description = "Submit your ncov information (a.k.a '疫情通')")]
        public async Task NCov(
            [Option('u', Description = "Specify your student id number")]
            string username,
            [Option('p', Description = "Specify your password")]
            string password,
            [Option('k', Description = "Specify your Serverchan key")]
            string sckey = "")
        {
            try
            {
                Console.WriteLine("- Logging in...");
                var (loginSuccessfully, loginErrMsg) = await NCovRepository.Instance.Login(username, password);
                if (!loginSuccessfully)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to login ({loginErrMsg}). Check your username and password.\n" +
                                      "If you are sure that your authentication is correct, contact the author for help.");
                    Console.ResetColor();
                    await SendNotification(sckey, "疫情通填写失败",
                        $"无法登录疫情通系统: {loginErrMsg}。请检查用户名和密码。如果确认信息正确，请联系作者。");
                    return;
                }

                Console.WriteLine("- Checking...");
                var isReported = await NCovRepository.Instance.CheckIsReported();
                if (isReported)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("You have submitted your information today.");
                    Console.ResetColor();
                    return;
                }

                var oldInfo = await NCovRepository.Instance.GetOldInfo();
                if (oldInfo == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(
                        "Failed to parse your information submitted before. Contact the author for help.");
                    Console.ResetColor();
                    await SendNotification(sckey, "疫情通填写失败", "无法解析前一日所填信息。请联系作者。");
                    return;
                }

                // Set temperature
                if (oldInfo.ContainsKey("tw"))
                    oldInfo["tw"] = "3";
                else
                    oldInfo.Add("tw", "3");
                // Add params
                if (!oldInfo.ContainsKey("ismoved"))
                    oldInfo.Add("ismoved", "0");
                if (!oldInfo.ContainsKey("szgjcs"))
                    oldInfo.Add("szgjcs", "");
                if (!oldInfo.ContainsKey("mjry"))
                    oldInfo.Add("mjry", "0");
                if (!oldInfo.ContainsKey("csmjry"))
                    oldInfo.Add("csmjry", "0");
                if (!oldInfo.ContainsKey("zgfxdq"))
                    oldInfo.Add("zgfxdq", "0");
                // Parse the geolocation info submitted before
                var geolocationInfo = JsonDocument.Parse(oldInfo["geo_api_info"].ToString()).RootElement;
                var province = geolocationInfo.GetProperty("addressComponent").GetProperty("province").GetString();
                var city = geolocationInfo.GetProperty("addressComponent").GetProperty("city").GetString();
                var area =
                    $"{province} {city} {geolocationInfo.GetProperty("addressComponent").GetProperty("district").GetString()}";
                // Complete the information to be submitted
                var newInfo = oldInfo.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
                var municipalities = new[] {"北京市", "天津市", "上海市", "重庆市"};
                newInfo["province"] = province;
                newInfo["city"] = municipalities.Contains(province) ? province : city;
                newInfo["area"] = area;
                newInfo["date"] = $"{DateTimeOffset.Now:yyyyMMdd}";
                newInfo["created"] = $"{DateTimeOffset.Now.ToUnixTimeSeconds()}";

                Console.WriteLine("- Submitting...");
                var (submitSuccessfully, submitErrMsg) = await NCovRepository.Instance.Submit(newInfo);
                if (submitSuccessfully)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Submitted successfully!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Submitted unsuccessfully: {submitErrMsg}\n" +
                                      "Contact the author for help.");
                    await SendNotification(sckey, "疫情通填写失败", $"信息提交失败: {submitErrMsg}。请联系作者。");
                }

                Console.ResetColor();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SendNotification(sckey, "自动疫情通运行失败", "请自行检查填写状态。有关运行失败的信息，请联系作者。");
            }
        }

        [Command("tcheck", Description = "Submit your tcheck information (a.k.a '晨午晚检')")]
        public async Task TCheck(
            [Option('u', Description = "Specify your student id number")]
            string username,
            [Option('p', Description = "Specify your password")]
            string password,
            [Option('c', Description = "Specify your campus, N or S is acceptable")]
            char campus,
            [Option('k', Description = "Specify your Serverchan key")]
            string sckey = "")
        {
            try
            {
                Console.WriteLine("- Logging in...");
                var (loginSuccessfully, loginErrMsg) = await TCheckRepository.Instance.Login(username, password);
                if (!loginSuccessfully)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Failed to login ({loginErrMsg}). Check your username and password.\n" +
                                      "If you are sure that your authentication is correct, contact the author for help.");
                    Console.ResetColor();
                    await SendNotification(sckey, "晨午晚检填写失败",
                        $"无法登录晨午晚检系统: {loginErrMsg}。请检查用户名和密码。如果确认信息正确，请联系作者。");
                    return;
                }

                Console.WriteLine("- Checking...");
                var isReported = await TCheckRepository.Instance.CheckIsReported();
                if (isReported)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("You have submitted your information today.");
                    Console.ResetColor();
                    return;
                }

                // Build new information
                var address = char.ToLower(campus) == 'n'
                    ? "陕西省西安市雁塔区电子城街道西安电子科技大学北校区"
                    : "陕西省西安市长安区兴隆街道西太一级公路西安电子科技大学长安校区";
                var geoApiInfo = char.ToLower(campus) == 'n'
                    ? "{\"type\":\"complete\",\"info\":\"SUCCESS\",\"status\":1,\"position\":{\"Q\":34.23254,\"R\":108.91516000000001,\"lng\":108.91800,\"lat\":34.23230},\"message\":\"Get ipLocation success.Get address success.\",\"location_type\":\"ip\",\"accuracy\":null,\"isConverted\":true,\"addressComponent\":{\"citycode\":\"029\",\"adcode\":\"610113\",\"businessAreas\":[],\"neighborhoodType\":\"\",\"neighborhood\":\"\",\"building\":\"\",\"buildingType\":\"\",\"street\":\"白沙路\",\"streetNumber\":\"付8号\",\"country\":\"中国\",\"province\":\"陕西省\",\"city\":\"西安市\",\"district\":\"雁塔区\",\"township\":\"电子城街道\"},\"formattedAddress\":\"陕西省西安市雁塔区电子城街道西安电子科技大学北校区\",\"roads\":[],\"crosses\":[],\"pois\":[]}"
                    : "{\"type\":\"complete\",\"position\":{\"Q\":34.131035970053,\"R\":108.83058024088598,\"lng\":108.83058,\"lat\":34.131036},\"location_type\":\"html5\",\"message\":\"Get geolocation success.Convert Success.Get address success.\",\"accuracy\":220,\"isConverted\":true,\"status\":1,\"addressComponent\":{\"citycode\":\"029\",\"adcode\":\"610116\",\"businessAreas\":[],\"neighborhoodType\":\"\",\"neighborhood\":\"\",\"building\":\"\",\"buildingType\":\"\",\"street\":\"雷甘路\",\"streetNumber\":\"266#\",\"country\":\"中国\",\"province\":\"陕西省\",\"city\":\"西安市\",\"district\":\"长安区\",\"township\":\"兴隆街道\"},\"formattedAddress\":\"陕西省西安市长安区兴隆街道西太一级公路西安电子科技大学长安校区\",\"roads\":[],\"crosses\":[],\"pois\":[],\"info\":\"SUCCESS\"}";
                var area = char.ToLower(campus) == 'n'
                    ? "陕西省 西安市 雁塔区"
                    : "陕西省 西安市 长安区";
                var submitParams = new Dictionary<string, string>
                {
                    {"city", "西安市"},
                    {"province", "陕西省"},
                    {"address", address},
                    {
                        "geo_api_info",
                        geoApiInfo
                    },
                    {"area", area},
                    {"tw", "2"},
                    {"sfzx", "1"},
                    {"sfcyglq", "0"},
                    {"sfyzz", "0"},
                    {"qtqk", ""},
                    {"ymtys", "0"}
                };
                // Submit
                Console.WriteLine("- Submitting...");
                var (successful, errMsg) = await TCheckRepository.Instance.Submit(submitParams);
                if (successful)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Submitted successfully!");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Submitted unsuccessfully: {errMsg}\n" +
                                      "Contact the author for help.");
                    await SendNotification(sckey, "晨午晚检填写失败", $"信息提交失败: {errMsg}。请联系作者。");
                }

                Console.ResetColor();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await SendNotification(sckey, "自动晨午晚检运行失败", "请自行检查填写状态。有关运行失败的信息，请联系作者。");
            }
        }

        private async Task SendNotification(string sckey, string title, string content)
        {
            if (sckey == "")
                return;

            await ServerchanRepository.Instance.SendMessage(sckey, title, content);
        }
    }
}