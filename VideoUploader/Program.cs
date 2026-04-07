using Azure.Storage.Blobs;
using VideoUploader;

const string VideosDirectory = @"C:\videos";
const string ConnectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04);

var blobServiceClient = new BlobServiceClient(ConnectionString, options);
//var blobServiceClient = new BlobServiceClient(ConnectionString);
var service = new VideoUploadService(blobServiceClient);

Console.WriteLine($"Scanning {VideosDirectory} for .mp4 files...");
await service.UploadVideosAsync(VideosDirectory);
Console.WriteLine("Done.");
