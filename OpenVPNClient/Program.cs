using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace OpenVPNClient
{
    class Program
    {
        // Service name
        private static readonly string RHYA_NETWORK_SERVICE_NAME = "kro_kr_rhya__network_vpn_service";
        // RHYA.Network Web Address
        private static readonly string RHYA_NETWORK_MAIN_WEB_ADDRESS = "https://rhya-network.kro.kr/";
        // RHYA.Network base URL
        private static readonly string RHYA_NETWORK_VPN_SERVICE_URL = "https://rhya-network.kro.kr/RhyaNetwork/vpn_access_manager";
        private static readonly string RHYA_NETWORK_LOGIN_URL = "https://rhya-network.kro.kr/RhyaNetwork/webpage/jsp/auth.v1/login_for_app.jsp";
        private static readonly string RHYA_NETWORK_AUTH_TOKEN_CREATE_URL = "https://rhya-network.kro.kr/RhyaNetwork/webpage/jsp/auth.v1/auth_token.jsp";
        private static readonly string RHYA_NETWORK_GET_USER_DATA_URL = "https://rhya-network.kro.kr/RhyaNetwork/webpage/jsp/auth.v1/auth_info.jsp";


        // Console Log Type Enum
        public enum ConsoleLogType
        {
            ERROR, WARNING, INFO, DEBUG
        }




        /// <summary>
        /// Main 함수 [ 프로그램 시작점 ]
        /// </summary>
        /// <param name="args">인자</param>
        static void Main(string[] args)
        {
            // 사용자 정보
            string uuid = null;
            string authToken = null;
            string userName = null;
            // OpenVPN 정보
            string openVPNConfig = null;
            string openVPNID = null;
            string openVPNPW = null;



            try
            {
                Console.WriteLine("RHYA.Network OpenVPN Client");
                Console.WriteLine("Welcome to the 'RHYA.Network OpenVPN Client'");
                Console.WriteLine("Web address: %url%".Replace("%url%", RHYA_NETWORK_MAIN_WEB_ADDRESS));
                Console.WriteLine("Client version : %version%, RHYA.Network VPN Tool".Replace("%version%", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString()));
                Console.WriteLine("");
                Console.WriteLine("Copyright RHYA.Network 2022, All rights reserved.");
                Console.WriteLine("");
                Console.WriteLine("This program is designed to use the RHYA.Network VPN service.");
                Console.WriteLine("You are responsible for any problems caused by the use of this program.");
                Console.WriteLine("To use this service, you need an RHYA.Network account and permission to use it.");
                Console.WriteLine("");

                consoleLogWriter(ConsoleLogType.INFO, "LOGIN", "RHYA.Network 계정으로 로그인해주세요.");

                // 로그인
                while (true)
                {
                    consoleLogWriter("LOGIN", "ID");
                    string userID = Console.ReadLine();
                    consoleLogWriter("LOGIN", "PASSWORD");
                    string userPW = Console.ReadLine();

                    consoleLogWriter(ConsoleLogType.INFO, "LOGIN", "RHYA.Network 로그인 중...");

                    string token = null;

                    WebClient webClient = new WebClient();
                    Stream stream = webClient.OpenRead(getFullServerUrl(-4, new Dictionary<string, string> { { "id", userID }, { "password", userPW }, { "ctoken", "1" } }));
                    JObject jObject = JObject.Parse(new StreamReader(stream).ReadToEnd());
                    stream.Dispose();
                    if (jObject.ContainsKey("result"))
                    {
                        if (jObject["result"].ToString().Equals("S") && jObject.ContainsKey("token") && jObject.ContainsKey("uuid"))
                        {
                            token = jObject["token"].ToString();
                            uuid = jObject["uuid"].ToString();

                            Stream temp = webClient.OpenRead(getFullServerUrl(-2, new Dictionary<string, string> { { "token", token }, { "user", uuid } }));
                            jObject = JObject.Parse(new StreamReader(temp).ReadToEnd());
                            temp.Dispose();
                            if (jObject.ContainsKey("result") && jObject.ContainsKey("message") && jObject["result"].ToString().Equals("success"))
                            {
                                authToken = jObject["message"].ToString();

                                Stream temp2 = webClient.OpenRead(getFullServerUrl(-3, new Dictionary<string, string> { { "token", authToken } }));
                                jObject = JObject.Parse(HttpUtility.UrlDecode(new StreamReader(temp2).ReadToEnd(), Encoding.UTF8));
                                temp2.Dispose();


                                if (jObject.ContainsKey("id"))
                                {
                                    userName = jObject["id"].ToString();
                                    consoleLogWriter(ConsoleLogType.INFO, "LOGIN", string.Format("Welcome {0}!!", userName));
                                    break;
                                }
                                else
                                {
                                    consoleLogWriter(ConsoleLogType.WARNING, "GET-USER-INFO", "알 수 없는 오류가 발생하였습니다.");
                                }
                            }
                            else
                            {
                                consoleLogWriter(ConsoleLogType.WARNING, "AUTH-TOKEN", "알 수 없는 오류가 발생하였습니다.");
                            }
                        }
                        else
                        {
                            if (jObject.ContainsKey("message"))
                                consoleLogWriter(ConsoleLogType.WARNING, "LOGIN", jObject["message"].ToString());
                            else
                                consoleLogWriter(ConsoleLogType.WARNING, "LOGIN", "알 수 없는 오류가 발생하였습니다.");
                        }
                    }
                    else
                    {
                        consoleLogWriter(ConsoleLogType.WARNING, "LOGIN", "알 수 없는 오류가 발생하였습니다.");
                    }
                }

                // VPN 정보 불러오기
                consoleLogWriter(ConsoleLogType.INFO, "GET-VPN-INFO", "OpenVPN 서버 정보 불러오는 중...");
                JObject openVPNInfoJObject = JObject.Parse(getOpenVPNServerInfo(authToken));
                if (openVPNInfoJObject.ContainsKey("result") && openVPNInfoJObject.ContainsKey("open_vpn_config") && openVPNInfoJObject.ContainsKey("account_id") && openVPNInfoJObject.ContainsKey("account_pw") && openVPNInfoJObject["result"].ToString().Equals("success"))
                {
                    openVPNConfig = openVPNInfoJObject["open_vpn_config"].ToString();
                    openVPNConfig = HttpUtility.UrlDecode(openVPNConfig, Encoding.UTF8);
                    openVPNConfig = Encoding.UTF8.GetString(Convert.FromBase64String(openVPNConfig));
                    openVPNID = openVPNInfoJObject["account_id"].ToString();
                    openVPNPW = HttpUtility.UrlDecode(openVPNInfoJObject["account_pw"].ToString(), Encoding.UTF8);
                }
                else
                {
                    if (openVPNInfoJObject.ContainsKey("message"))
                        consoleLogWriter(ConsoleLogType.WARNING, "GET-VPN-INFO", HttpUtility.UrlDecode(openVPNInfoJObject["message"].ToString(), Encoding.UTF8));
                    else
                        consoleLogWriter(ConsoleLogType.WARNING, "GET-VPN-INFO", "알 수 없는 오류가 발생하였습니다.");
                }
                consoleLogWriter(ConsoleLogType.INFO, "GET-VPN-INFO", "OpenVPN 서버 정보 불러오기 성공!");

                // OpenVPN 설치 확인
                consoleLogWriter(ConsoleLogType.INFO, "INSTALL-OPEN-VPN", "OpenVPN 설치 확인 중...");
                consoleLogWriter(ConsoleLogType.WARNING, "INSTALL-OPEN-VPN", @"OpenVPN은 기본경로에 설치되어있어야 합니다. (C:\Program Files\OpenVPN\)");

                if (!new System.IO.FileInfo(@"C:\Program Files\OpenVPN\bin\openvpn.exe").Exists)
                {
                    OpenVPNBase64 openVPNBase64 = new OpenVPNBase64();
                    System.IO.File.WriteAllBytes("setup.msi", Convert.FromBase64String(openVPNBase64.base64));

                    Process.Start("setup.msi");

                    while (true)
                    {
                        if (new System.IO.FileInfo(@"C:\Program Files\OpenVPN\bin\openvpn.exe").Exists) break;

                        consoleLogWriter(ConsoleLogType.INFO, "INSTALL-OPEN-VPN", "OpenVPN 설치 대기 중...  (1500ms)");

                        Thread.Sleep(1500);
                    }

                    Console.Write("설치 완료 시 아무 키나 눌러주세요...");
                    Console.ReadKey();
                    Console.WriteLine("");
                }

                consoleLogWriter(ConsoleLogType.INFO, "OPEN-VPN-SETTING", "OpenVPN 설정 중...");
                System.IO.File.WriteAllText(@"C:\Program Files\OpenVPN\bin\RHYA.Network_OpenVPN_Config.ovpn", openVPNConfig);
                System.IO.File.WriteAllText(@"C:\Program Files\OpenVPN\bin\RHYA.Network_OpenVPN_UserInfo.rhya", string.Format("{0}\n\r{1}", openVPNID, openVPNPW));

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.WorkingDirectory = @"C:\Program Files\OpenVPN\bin";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = false;
                psi.FileName = @"C:\Program Files\OpenVPN\bin\openvpn.exe";
                psi.Arguments = "RHYA.Network_OpenVPN_Config.ovpn";

                Process rootProcess = new Process();
                rootProcess = Process.Start(psi);
                rootProcess.WaitForExit();
                rootProcess.Close();
                rootProcess.Dispose();
                rootProcess = null;
            }
            catch (Exception ex)
            {
                consoleLogWriter(ConsoleLogType.ERROR, "EXCEPTION", ex.ToString());
                throw ex;
            }
        }



        /// <summary>
        /// Console Log 출력
        /// </summary>
        /// <param name="consoleLogType">로그 형식</param>
        /// <param name="title">로그 제목</param>
        /// <param name="message">로그 메시지</param>
        private static void consoleLogWriter(ConsoleLogType consoleLogType, string title, string message)
        {
            try
            {
                string logType = null;
                switch (consoleLogType)
                {
                    case ConsoleLogType.ERROR:
                        logType = "ERROR";
                        break;
                    case ConsoleLogType.WARNING:
                        logType = "WARNING";
                        break;
                    case ConsoleLogType.INFO:
                        logType = "INFO";
                        break;
                    case ConsoleLogType.DEBUG:
                        logType = "DEBUG";
                        break;
                }

                Console.WriteLine(string.Format("[{0} / {1}]  {2,-15}   ~$ {3}", DateTime.Now.ToString(), logType, title, message));
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }



        /// <summary>
        /// Console Log 출력
        /// </summary>
        /// <param name="title">제목</param>
        /// <param name="message">메시지</param>
        private static void consoleLogWriter(string title, string message)
        {
            try
            {
                Console.Write(string.Format("{0}@{1}: ~$ ", title, message));
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }



        /// <summary>
        /// OpenVPN 서버 정보 불러오기
        /// </summary>
        /// <param name="authToken">Auth Token</param>
        /// <returns></returns>
        private static string getOpenVPNServerInfo(string authToken)
        {
            WebClient webClient = new WebClient();
            Stream stream = webClient.OpenRead(getFullServerUrl(0, new Dictionary<string, string> { { "auth", authToken } }));
            return new StreamReader(stream).ReadToEnd();
        }



        /// <summary>
        /// 접속 URL 생성 함수
        /// </summary>
        /// <param name="mode">명령어 종류</param>
        /// <param name="parms">명령어 인자</param>
        /// <returns>RHYA.Network Service Utaite Player UURL</returns>
        private static string getFullServerUrl(int mode, Dictionary<string, string> parms)
        {
            try
            {
                int index = 0;
                StringBuilder stringBuilder = new StringBuilder();


                switch (mode)
                {
                    // VPN ACCESS 관리자 URL
                    // ------------------------------------------------------
                    default:
                        stringBuilder.Append(RHYA_NETWORK_VPN_SERVICE_URL);
                        // 파라미터 설정
                        stringBuilder.Append("?");
                        stringBuilder.Append("mode=");
                        stringBuilder.Append(mode);

                        break;
                    // ------------------------------------------------------



                    // Auth Token 발급 URL
                    // ------------------------------------------------------
                    case -2:
                        stringBuilder.Append(RHYA_NETWORK_AUTH_TOKEN_CREATE_URL);
                        // 파라미터 설정
                        stringBuilder.Append("?");
                        stringBuilder.Append("name=");
                        stringBuilder.Append(RHYA_NETWORK_SERVICE_NAME);
                        break;
                    // ------------------------------------------------------



                    // 사용자 정보 가져오기 URL
                    // ------------------------------------------------------
                    case -3:
                        stringBuilder.Append(RHYA_NETWORK_GET_USER_DATA_URL);
                        // 파라미터 설정
                        stringBuilder.Append("?");
                        stringBuilder.Append("name=");
                        stringBuilder.Append(RHYA_NETWORK_SERVICE_NAME);
                        break;
                    // ------------------------------------------------------



                    // 사용자 로그인 URL
                    // ------------------------------------------------------
                    case -4:
                        stringBuilder.Append(RHYA_NETWORK_LOGIN_URL);
                        // 파라미터 설정
                        stringBuilder.Append("?");
                        stringBuilder.Append("name=");
                        stringBuilder.Append(RHYA_NETWORK_SERVICE_NAME);
                        break;
                   // ------------------------------------------------------
                }


                // Null 확인
                if (parms != null)
                {
                    // 추가 파라미터 합치기
                    foreach (string key in parms.Keys)
                    {
                        stringBuilder.Append("&");
                        stringBuilder.Append(key);
                        stringBuilder.Append("=");
                        stringBuilder.Append(parms[key]);

                        checked
                        {
                            index += 1;
                        }
                    }
                }


                return stringBuilder.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
