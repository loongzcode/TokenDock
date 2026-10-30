using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TokenDock
{
    sealed class UpdateRelease
    {
        public const string RepositoryUrl = "https://github.com/loongzcode/TokenDock";
        public const long MaximumPackageSize = 64 * 1024 * 1024;
        public Version Version { get; private set; }
        public string Tag { get; private set; }
        public string FileName { get; private set; }
        public string PackageUrl { get; private set; }
        public string ChecksumsUrl { get; private set; }
        public string Digest { get; private set; }
        public long Size { get; private set; }
        public string PageUrl { get { return RepositoryUrl + "/releases/tag/" + Tag; } }

        public static UpdateRelease Parse(object json)
        {
            string tag = Json.Text(Json.Get(json, "tag_name"));
            Version version;
            if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") || !System.Version.TryParse(tag.TrimStart('v'), out version) || Json.Text(Json.Get(json, "draft")) != "False" || Json.Text(Json.Get(json, "prerelease")) != "False")
                throw new InvalidDataException("未找到有效的正式版本。");
            string name = "TokenDock-" + version.ToString(3) + ".zip";
            var assets = Json.Items(Json.Get(json, "assets")).ToArray();
            var packages = assets.Where(a => Json.Text(Json.Get(a, "name")) == name).ToArray();
            var checksums = assets.Where(a => Json.Text(Json.Get(a, "name")) == "SHA256SUMS.txt").ToArray();
            if (packages.Length != 1 || checksums.Length != 1)
                throw new InvalidDataException("该版本缺少运行包或 SHA256 校验文件。");
            string prefix = RepositoryUrl + "/releases/download/" + tag + "/";
            string packageUrl = Json.Text(Json.Get(packages[0], "browser_download_url"));
            string checksumsUrl = Json.Text(Json.Get(checksums[0], "browser_download_url"));
            long size = Json.Integer(Json.Get(packages[0], "size")) ?? 0;
            string digest = Json.Text(Json.Get(packages[0], "digest"));
            if (packageUrl != prefix + name || checksumsUrl != prefix + "SHA256SUMS.txt" || size <= 0 || size > MaximumPackageSize || (digest.Length > 0 && !Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$")))
                throw new InvalidDataException("更新附件信息不完整或来源不匹配。");
            return new UpdateRelease { Version = version, Tag = tag, FileName = name, PackageUrl = packageUrl, ChecksumsUrl = checksumsUrl, Size = size, Digest = digest };
        }

        public bool IsNewerThan(Version installed)
        {
            return Version > new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
        }

        internal static UpdateRelease FromPublicPage(Uri page, long size)
        {
            string prefix = RepositoryUrl + "/releases/tag/";
            if (page == null || !page.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("未找到仓库的正式发布页。");
            string tag = page.AbsoluteUri.Substring(prefix.Length);
            Version version;
            if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") || !System.Version.TryParse(tag.TrimStart('v'), out version) || size < 0 || size > MaximumPackageSize)
                throw new InvalidDataException("未找到有效的正式发布版本。");
            string name = "TokenDock-" + version.ToString(3) + ".zip";
            string assetPrefix = RepositoryUrl + "/releases/download/" + tag + "/";
            return new UpdateRelease { Version = version, Tag = tag, FileName = name, PackageUrl = assetPrefix + name, ChecksumsUrl = assetPrefix + "SHA256SUMS.txt", Size = size, Digest = "" };
        }

        public string ReadChecksum(string text)
        {
            var matches = text.Split('\n').Select(line => Regex.Match(line.TrimEnd('\r'), @"^([a-fA-F0-9]{64})[ \t]+\*?" + Regex.Escape(FileName) + "$" )).Where(match => match.Success).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException("校验文件中缺少唯一匹配的运行包记录。");
            string hash = matches[0].Groups[1].Value.ToLowerInvariant();
            if (Digest.Length > 0 && !string.Equals(Digest, "sha256:" + hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("GitHub 附件摘要与校验文件不一致。");
            return hash;
        }
    }
}
