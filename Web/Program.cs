//アニメカレンダー

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using IniParser;

namespace GenshinEventChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            var ini = new FileIniDataParser().ReadFile("./setting.ini");

            var start_url = "https://www.animatetimes.com/tag/details.php?id=1392";
            var driver_dir = ini["Directory"]["driver"].Trim('"');
            var binary_dir = ini["Directory"]["binary"].Trim('"');

            var service = ChromeDriverService.CreateDefaultService(driver_dir);
            service.HideCommandPromptWindow = true;
            var options = new ChromeOptions();
            options.BinaryLocation = binary_dir;

            //起動オプション
            var arguments = new string[] {
                //"--user-data-dir=" + user_dir, 
                //"--profile-directory=Default",
                "--app=" + start_url, //アプリケーションモードで起動
                "--incognito", //シークレットモードで起動
                "--start-maximized", //最大化(--window-position, --window-sizeと併用不可)
                //"--window-size=" + ((int)session["right"] - (int)session["left"]).ToString() + ','+((int)session["bottom"] - (int)session["top"]).ToString(), //ウィンドウサイズ
                //"--window-position=" + (session["left"]).ToString() + ',' + (session["top"]).ToString(), //ウィンドウ位置
                //"--headless=new", //ヘッドレスモードを有効化
                "--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36", //ヘッドレスモードの場合はUAの指定が必要（無いとページロードのタイムアウトが発生）
                "--enable-parallel-downloading", //#並列ダウンロードを有効化
                "--enable-quic", //QUICプロトコルを有効化
                "--test-type=gpu", //アドレスバー下に表示される「Chrome for Testing...」を非表示
                "--hide-scrollbars", //スクロールバー非表示
                "--mute-audio", //ミュート
                "--disable-background-networking", //拡張機能の更新、セーフブラウジングサービス、アップグレード検出、翻訳、UMAを含む様々なバックグラウンドネットワークサービスを無効化
                "--ignore-certificate-errors", //SSL認証(この接続ではプライバシーが保護されません)を無効化
            };
            options.AddArguments(arguments);
            options.AddExcludedArgument("enable-automation"); //「自動テストソフトウェアによって制御されています」非表示

            IWebDriver driver = new ChromeDriver(service, options);

            try
            {
                //driver.Url = start_url;
                var anime_info = new Dictionary<string, List<Dictionary<string, string>>>(){
                    {"日", new List<Dictionary<string, string>>() },
                    {"月", new List<Dictionary<string, string>>() },
                    {"火", new List<Dictionary<string, string>>() },
                    {"水", new List<Dictionary<string, string>>() },
                    {"木", new List<Dictionary<string, string>>() },
                    {"金", new List<Dictionary<string, string>>() },
                    {"土", new List<Dictionary<string, string>>() },
                };

                var blog_cards = WaitPresenceClass(driver, "blog-card");
                foreach (var card in blog_cards)
                {
                    try
                    {
                        var title = card.FindElement(By.ClassName("blog-title")).Text;
                        if (title.Contains("配信サブスク情報まとめ"))
                            continue;
                        var detail = card.FindElement(By.ClassName("blog-description")).Text.Split(new string[] {
                            "作品名",
                            "放送形態",
                            "スケジュール",
                            "キャスト"
                        }, StringSplitOptions.None);
                        if (detail.Length < 4)
                            continue;
                        var link = card.FindElement(By.ClassName("blog-card-link")).GetAttribute("href");
                        var image = card.FindElement(By.ClassName("blog-img")).GetAttribute("style").Split(new string[] { "(", ")" }, StringSplitOptions.None)[1].Trim('"');
                        if (image == string.Empty)
                            image = "https://www.shoshinsha-design.com/wp-content/uploads/2022/02/noimage_アイコン-760x460.png";
                        var week = detail[3].Split(new string[] { "（", "）" }, StringSplitOptions.None)[1];

                        anime_info[week].Add(new Dictionary<string, string> {
                            { "title", title },
                            { "link", link },
                            { "description", detail[0] },
                            { "image", image }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }

                /*Console.WriteLine("title: {0}", anime_info["月"][0]["title"]);
                Console.WriteLine("link: {0}", anime_info["月"][0]["link"]);
                Console.WriteLine("description: {0}", anime_info["月"][0]["description"]);
                Console.WriteLine("image: {0}", anime_info["月"][0]["image"]);*/

                driver.Url = Directory.GetCurrentDirectory() + "/Template.html"; //カレンダーテンプレート
                WaitVisibilityClass(driver, "calendar");

                //カレンダー編集
                var days = new string[] { "日", "月", "火", "水", "木", "金", "土" };

                foreach (var day in days)
                {
                    for (int i = 0; i < anime_info[day].Count; i++)
                    {
                        var target_cell = driver.FindElements(By.ClassName("row"))[i].FindElements(By.ClassName("column"))[Array.IndexOf(days, day)];
                        var title = target_cell.FindElement(By.ClassName("anime-title"));
                        var link = target_cell.FindElement(By.ClassName("anime-link"));
                        var image = target_cell.FindElement(By.ClassName("anime-img"));

                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].textContent = arguments[1];", title, anime_info[day][i]["title"]);
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].setAttribute('href', arguments[1]);", link, anime_info[day][i]["link"]);
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].setAttribute('title', arguments[1]);", image, anime_info[day][i]["description"]);
                        ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].setAttribute('src', arguments[1]);", image, anime_info[day][i]["image"]);
                    }
                }

                //html保存（うまくいってない）
                /*var mhtml_data = ((ChromeDriver)driver).ExecuteCdpCommand("Page.captureSnapshot", new Dictionary<string, object>{ { "format", "mhtml" } });
                var mhtml_text = ((Dictionary<string, object>)mhtml_data)["data"].ToString();
                using (var sw = new StreamWriter("./AnimeCalendar.html", false, Encoding.UTF8))
                {
                    sw.Write(mhtml_text);
                }*/

                string current_url;
                while (true)
                {
                    Thread.Sleep(1000);
                    current_url = driver.Url;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                driver.Quit();
            }
        }

        //Class要素表示まで待機
        static ReadOnlyCollection<IWebElement> WaitVisibilityClass(IWebDriver driver, string class_name, int timeout = 5)
        {
            return new WebDriverWait(driver, new TimeSpan(0, 0, timeout)).Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.ClassName(class_name)));
        }

        //ID要素表示まで待機
        static ReadOnlyCollection<IWebElement> WaitVisibilityId(IWebDriver driver, string id_name, int timeout = 5)
        {
            return new WebDriverWait(driver, new TimeSpan(0, 0, timeout)).Until(ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.Id(id_name)));
        }





        //Class要素存在まで待機
        static ReadOnlyCollection<IWebElement> WaitPresenceClass(IWebDriver driver, string class_name, int timeout = 5)
        {
            return new WebDriverWait(driver, new TimeSpan(0, 0, timeout)).Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.ClassName(class_name)));
        }

        //ID要素存在まで待機
        static ReadOnlyCollection<IWebElement> WaitPresenceId(IWebDriver driver, string id_name, int timeout = 5)
        {
            return new WebDriverWait(driver, new TimeSpan(0, 0, timeout)).Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.Id(id_name)));
        }




        //Class要素クリック
        static void ClickClass(IWebDriver driver, string class_name, int index = 0, int timeout = 5)
        {
            new WebDriverWait(driver, new TimeSpan(0, 0, timeout)).Until(ExpectedConditions.ElementToBeClickable(By.ClassName(class_name)));
            driver.FindElements(By.ClassName(class_name))[index].Click();
        }

        //ID要素クリック
        static void ClickId(IWebDriver driver, string id_name, int index = 0, int timeout = 5)
        {
            new WebDriverWait(driver, new TimeSpan(0, 0, timeout)).Until(ExpectedConditions.ElementToBeClickable(By.Id(id_name)));
            driver.FindElements(By.ClassName(id_name))[index].Click();
        }
    }
}
