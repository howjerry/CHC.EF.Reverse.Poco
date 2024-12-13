using CommandLine;
using System;
using System.IO;

namespace CHC.EF.Reverse.Poco.Core.Configuration
{
    /// <summary>
    /// 定義應用程式的命令列參數選項。
    /// </summary>
    /// <remarks>
    /// 此類別使用 CommandLine 套件來解析命令列參數，提供資料庫反向工程所需的各項設定。
    /// 所有屬性都提供預設值或可為空的設定，確保應用程式在缺少某些參數時仍能正常運作。
    /// </remarks>
    public class Options
    {
        /// <summary>
        /// 取得或設定資料庫連線字串。
        /// </summary>
        /// <remarks>
        /// 連線字串應包含資料庫伺服器位址、認證資訊及目標資料庫名稱。
        /// 若未在命令列指定，將嘗試從組態檔讀取。
        /// </remarks>
        [Option('c', "connection", Required = false,
            HelpText = "資料庫連線字串，包含伺服器位址、認證資訊及資料庫名稱")]
        public string ConnectionString { get; set; }

        /// <summary>
        /// 取得或設定資料庫提供者名稱。
        /// </summary>
        /// <remarks>
        /// 支援的值包括：
        /// - SqlServer：Microsoft SQL Server
        /// - MySql：MySQL 資料庫
        /// 若未指定，預設使用 SQL Server。
        /// </remarks>
        [Option('p', "provider", Required = false,
            HelpText = "資料庫提供者 (SqlServer/MySql)")]
        public string Provider { get; set; }

        /// <summary>
        /// 取得或設定產生程式碼的命名空間。
        /// </summary>
        /// <remarks>
        /// 指定產生的實體類別、DbContext 等程式碼的命名空間。
        /// 若未指定，將使用預設值 "GeneratedApp.Data"。
        /// </remarks>
        [Option('n', "namespace", Required = false,
            HelpText = "產生程式碼的命名空間")]
        public string Namespace { get; set; }

        /// <summary>
        /// 取得或設定程式碼輸出目錄。
        /// </summary>
        /// <remarks>
        /// 指定產生的程式碼檔案要儲存的目錄路徑。
        /// 若目錄不存在，將自動建立。
        /// </remarks>
        [Option('o', "output", Required = false,
            HelpText = "程式碼輸出目錄路徑")]
        public string OutputDirectory { get; set; }

        /// <summary>
        /// 取得或設定是否要將集合名稱轉換為複數形式。
        /// </summary>
        /// <remarks>
        /// 影響 DbSet 屬性名稱及導航屬性的命名。
        /// 例如：Customer -> Customers
        /// </remarks>
        [Option("pluralize", Required = false,
            HelpText = "是否將集合名稱轉換為複數形式")]
        public bool? IsPluralize { get; set; }

        /// <summary>
        /// 取得或設定是否使用資料註解特性。
        /// </summary>
        /// <remarks>
        /// 若啟用，將在實體類別中加入 System.ComponentModel.DataAnnotations 的特性，
        /// 例如 [Required]、[StringLength] 等。
        /// </remarks>
        [Option("data-annotations", Required = false,
            HelpText = "是否使用資料註解特性")]
        public bool? UseDataAnnotations { get; set; }

        /// <summary>
        /// 取得或設定自訂組態檔的路徑。
        /// </summary>
        /// <remarks>
        /// 指定包含其他設定的 JSON 組態檔路徑。
        /// 預設值為 "appsettings.json"。
        /// 組態檔中的設定將與命令列參數合併，且命令列參數優先。
        /// </remarks>
        [Option("config", Required = false, Default = "appsettings.json",
            HelpText = "自訂組態檔路徑")]
        public string ConfigFile { get; set; }

        /// <summary>
        /// 驗證選項的有效性。
        /// </summary>
        /// <returns>若選項有效則返回 true，否則返回 false</returns>
        /// <remarks>
        /// 檢查必要參數是否已設定，以及參數值是否合法。
        /// </remarks>
        public bool Validate()
        {
            // 檢查連線字串
            if (string.IsNullOrEmpty(ConnectionString) &&
                string.IsNullOrEmpty(ConfigFile))
            {
                return false;
            }

            // 檢查提供者名稱
            if (!string.IsNullOrEmpty(Provider))
            {
                var normalizedProvider = Provider.ToLowerInvariant();
                if (normalizedProvider != "sqlserver" &&
                    normalizedProvider != "mysql")
                {
                    return false;
                }
            }

            // 檢查輸出目錄
            if (!string.IsNullOrEmpty(OutputDirectory))
            {
                try
                {
                    var fullPath = Path.GetFullPath(OutputDirectory);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }
    }
}