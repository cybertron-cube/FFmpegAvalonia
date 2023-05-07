using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using Amazon.S3.Transfer;
using Amazon.S3;
using Amazon;
using static Cybertron.GenStatic;
using FFmpegAvalonia.Models;

namespace FFmpegAvalonia.TaskTypes
{
    public class AWSTask
    {
        private string? _accessKeyId;
        private string? _secretAccessKey;
        private string? _lastFileName;
        private string? _bucketName;
        private string? _keyPrefix;
        private RegionEndpoint? _regionEndpoint;
        private IProgress<double>? _progress;
        public (bool, string) CheckConfigAndCredentials()
        {
            string credentialsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aws", "credentials");
            Trace.TraceInformation("Credentials Path: " + credentialsFilePath);
            string configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".aws", "config");
            Trace.TraceInformation("Config Path: " + configFilePath);

            if (!File.Exists(credentialsFilePath) || !File.Exists(configFilePath))
            {
                return (false, "The credentials or config file does not exist");
            }

            string credentialsFileText = File.ReadAllText(credentialsFilePath);
            ReplaceWinNewLine(ref credentialsFileText);//
            var reg = new Regex("(?:aws_access_key_id = )(.*)");
            var match = reg.Match(credentialsFileText);

            if (match.Success)
            {
                _accessKeyId = match.Groups[1].Value;
            }
            else return (false, "An access key id entry was not found within the credentials file");

            reg = new Regex("(?:aws_secret_access_key = )(.*)");
            match = reg.Match(credentialsFileText);

            if (match.Success) { _secretAccessKey = match.Groups[1].Value; }
            else return (false, "A secret access key entry was not found within the credentials file");

            string configFileText = File.ReadAllText(configFilePath);
            ReplaceWinNewLine(ref configFileText);//
            reg = new Regex("(?:region = )(.*)");
            match = reg.Match(configFileText);
            string region;
            if (match.Success) { region = match.Groups[1].Value; }
            else return (false, "A region entry was not found within the config file");
            _regionEndpoint = RegionEndpoint.GetBySystemName(region);
            return (true, string.Empty);
        }
        public bool AssignBucketNameAndKeyPrefix(string path)
        {
            var reg = new Regex("s3://(?<bucket>.*?)/(?<prefix>.*)");
            var match = reg.Match(path);
            if (match.Success)
            {
                _bucketName = match.Groups["bucket"].Value;
                _keyPrefix = match.Groups["prefix"].Value;
                return true;
            }
            else return false;
        }
        public async Task<string> UploadDirectoryAsync(ListViewData item, IProgress<double> progress, CancellationToken ct)
        {
            if (_accessKeyId == null || _secretAccessKey == null || _regionEndpoint == null)
            {
                throw new Exception("An access key id, secret access key, or region endpoint was not assigned");
            }
            var s3Client = new AmazonS3Client(_accessKeyId, _secretAccessKey, _regionEndpoint);
            var transferUtility = new TransferUtility(s3Client);
            var request = new TransferUtilityUploadDirectoryRequest
            {
                BucketName = _bucketName,
                KeyPrefix = _keyPrefix,
                Directory = item.Description.SourceDir,
                SearchPattern = $"*{item.Description.FileExt}"
            };
            _progress = progress;
            request.UploadDirectoryProgressEvent += Request_UploadDirectoryProgressEvent;
            try
            {
                await transferUtility.UploadDirectoryAsync(request, ct); 
            }
            catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
            {
                transferUtility.Dispose();
                s3Client.Dispose();
                return _lastFileName ??= String.Empty;
            }
            transferUtility.Dispose();
            s3Client.Dispose();
            return "0";
        }
        private void Request_UploadDirectoryProgressEvent(object? sender, UploadDirectoryProgressArgs e)
        {
            _progress?.Report((double)e.TransferredBytes / e.TotalBytes);
            if (_lastFileName != e.CurrentFile)
            {
                _lastFileName = e.CurrentFile;
            }
        }
    }
}
