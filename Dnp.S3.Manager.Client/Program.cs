// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Doughnuts Publishing LLC">
//     Author: Doug Hunt
//     Copyright (c)  Doughnuts Publishing LLC. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

var config = builder.Build();
var account = config.GetSection("Account");
var accessKey = account["AccessKey"];
var secretKey = account["SecretKey"];
var region = account["Region"];
var apiBase = config["ApiBase"] ?? "https://localhost:5001";

Console.WriteLine("S3 Manager API Test Harness");
Console.WriteLine($"API Base: {apiBase}");

using var http = new HttpClient();
http.BaseAddress = new Uri(apiBase);

async Task ListBuckets()
{
    var payload = new { AccessKey = accessKey, SecretKey = secretKey, Region = region };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    var res = await http.PostAsync("api/s3/buckets", content);
    Console.WriteLine(await res.Content.ReadAsStringAsync());
}

async Task ListObjects(string bucket)
{
    var payload = new { Account = new { AccessKey = accessKey, SecretKey = secretKey, Region = region }, Bucket = bucket };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    var res = await http.PostAsync("api/s3/list", content);
    Console.WriteLine(await res.Content.ReadAsStringAsync());
}

async Task UploadFile(string bucket, string key, string filePath)
{
    using var form = new MultipartFormDataContent();
    var accountJson = JsonSerializer.Serialize(new { AccessKey = accessKey, SecretKey = secretKey, Region = region });
    form.Add(new StringContent(accountJson, Encoding.UTF8, "application/json"), "Account");
    form.Add(new StringContent(bucket), "Bucket");
    form.Add(new StringContent(key), "Key");

    var fs = File.OpenRead(filePath);
    var streamContent = new StreamContent(fs);
    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    form.Add(streamContent, "File", Path.GetFileName(filePath));

    var res = await http.PostAsync("api/s3/upload", form);
    Console.WriteLine(res.StatusCode);
}

async Task DownloadFile(string bucket, string key, string dest)
{
    var url = $"api/s3/download?accessKey={Uri.EscapeDataString(accessKey)}&secretKey={Uri.EscapeDataString(secretKey)}&region={Uri.EscapeDataString(region)}&bucket={Uri.EscapeDataString(bucket)}&key={Uri.EscapeDataString(key)}";
    var res = await http.GetAsync(url);
    res.EnsureSuccessStatusCode();
    var bytes = await res.Content.ReadAsByteArrayAsync();
    await File.WriteAllBytesAsync(dest, bytes);
    Console.WriteLine($"Downloaded to {dest}");
}

async Task Rename(string bucket, string sourceKey, string destKey)
{
    var payload = new { Account = new { AccessKey = accessKey, SecretKey = secretKey, Region = region }, Bucket = bucket, SourceKey = sourceKey, DestinationKey = destKey };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    var res = await http.PostAsync("api/s3/rename", content);
    Console.WriteLine(res.StatusCode);
}

async Task Delete(string bucket, string[] keys)
{
    var payload = new { Account = new { AccessKey = accessKey, SecretKey = secretKey, Region = region }, Bucket = bucket, Keys = keys };
    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    var res = await http.PostAsync("api/s3/delete", content);
    Console.WriteLine(res.StatusCode);
}

// Simple interactive menu
while (true)
{
    Console.WriteLine("\nOptions: 1=ListBuckets 2=ListObjects 3=Upload 4=Download 5=Rename 6=Delete 0=Exit");
    var opt = Console.ReadLine();
    if (opt == "0") break;
    try
    {
        switch (opt)
        {
            case "1": await ListBuckets(); break;
            case "2": Console.Write("Bucket: "); var b = Console.ReadLine(); await ListObjects(b); break;
            case "3": Console.Write("Bucket: "); b = Console.ReadLine(); Console.Write("Key: "); var k = Console.ReadLine(); Console.Write("File Path: "); var p = Console.ReadLine(); await UploadFile(b, k, p); break;
            case "4": Console.Write("Bucket: "); b = Console.ReadLine(); Console.Write("Key: "); k = Console.ReadLine(); Console.Write("Destination path: "); var d = Console.ReadLine(); await DownloadFile(b, k, d); break;
            case "5": Console.Write("Bucket: "); b = Console.ReadLine(); Console.Write("Source Key: "); var s = Console.ReadLine(); Console.Write("Destination Key: "); var dest = Console.ReadLine(); await Rename(b, s, dest); break;
            case "6": Console.Write("Bucket: "); b = Console.ReadLine(); Console.Write("Keys (comma separated): "); var line = Console.ReadLine(); var keys = line.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); await Delete(b, keys); break;
            default: Console.WriteLine("Unknown option"); break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
