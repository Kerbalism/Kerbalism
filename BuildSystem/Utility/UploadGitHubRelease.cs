/*
Note : this class is compiled on the fly at build-time by the corresponding task in the MSBuildSystem.xml file.
It isn't part of the KerbalismBuild project but there is a bug on the mono version of msbuild preventing it from being elsewhere.
 */

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Web;

public class UploadGitHubRelease : Task
{
    public ITaskItem[] ZipFilesToUpload { get; set; }

    [Required]
    public string GithubOAuthToken { get; set; }

    [Required]
    public string GithubUser { get; set; }

    [Required]
    public string GithubRepo { get; set; }

    [Required]
    public string ReleaseName { get; set; }

    [Required]
    public string ReleaseTag { get; set; }

    [Required]
    public string ReleaseDescription { get; set; }

    [Required]
    public bool PreRelease { get; set; }

    [Required]
    public bool Draft { get; set; }

    public override bool Execute()
    {
        System.Threading.Tasks.Task.Run(() => Upload()).Wait();
        return !Log.HasLoggedErrors;
    }

    private async System.Threading.Tasks.Task Upload()
    {
        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("msbuild", "4.0"));
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", GithubOAuthToken);

            HttpResponseMessage createReleaseResponse;

            //File.WriteAllText(@"C:\Users\Got\Desktop\JSONTEST_beforetransform.txt", ReleaseDescription);

            string postUrl = @"https://api.github.com/repos/" + GithubUser + @"/" + GithubRepo + @"/releases";
            using (var request = new HttpRequestMessage(new HttpMethod("POST"), postUrl))
            {
                string json = @"{ ""tag_name"": """;
                json += ReleaseTag;
                json += @""", ""target_commitish"": ""master"", ""name"": """;
                json += ReleaseName;
                json += @""", ""body"": """;
                json += HttpUtility.JavaScriptStringEncode(ReleaseDescription, false);
                json += @""", ""draft"": ";
                json += Draft.ToString().ToLower();
                json += @", ""prerelease"": ";
                json += PreRelease.ToString().ToLower();
                json += @"}";

                //File.WriteAllText(@"C:\Users\Got\Desktop\JSONTEST_afterTransform.txt", json);

                //string json = @"{ ""tag_name"": ""v11.0.0"", ""target_commitish"": ""master"", ""name"": ""release name v11.0.0"", ""body"": ""Description of the release"", ""draft"": true, ""prerelease"": false}";
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                createReleaseResponse = await httpClient.SendAsync(request);
            }

            string createReleaseOutput = await createReleaseResponse.Content.ReadAsStringAsync();

            if (!createReleaseResponse.IsSuccessStatusCode)
            {
                Log.LogMessage(createReleaseResponse.ToString());
                Log.LogMessage(createReleaseOutput.ToString());
                Log.LogError("[UploadGitHubRelease] Failed to create release :");
                return;
            }

            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(createReleaseOutput), new System.Xml.XmlDictionaryReaderQuotas());

            var root = XElement.Load(jsonReader);
            string uploadURL = root.XPathSelectElement("//upload_url").Value;
            uploadURL = uploadURL.Replace("{?name,label}", "?name=");

            Log.LogMessage("[UploadGitHubRelease] Release '" + ReleaseName + "' created with tag '" + ReleaseTag + "'");

            if (ZipFilesToUpload == null) return;

            foreach (ITaskItem zipItem in ZipFilesToUpload)
            {
                HttpResponseMessage uploadZipResponse;
                string zipname = Path.GetFileName(zipItem.ItemSpec);
                Log.LogMessage("[UploadGitHubRelease] Uploading '" + zipname + "'...");
                using (var request = new HttpRequestMessage(new HttpMethod("POST"), uploadURL + zipname))
                {
                    request.Content = new ByteArrayContent(File.ReadAllBytes(zipItem.ItemSpec));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                    uploadZipResponse = await httpClient.SendAsync(request);
                }

                if (!uploadZipResponse.IsSuccessStatusCode)
                {
                    Log.LogMessage(uploadZipResponse.ToString());
                    Log.LogMessage(await uploadZipResponse.Content.ReadAsStringAsync());
                    Log.LogError("[UploadGitHubRelease] Failed to upload zip file : " + zipItem.ItemSpec);
                    break;
                }

                Log.LogMessage("[UploadGitHubRelease] " + zipname + " uploaded successfully");
            }
        }
    }
}
