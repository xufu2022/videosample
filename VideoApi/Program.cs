using Azure.Storage.Blobs;
using VideoApi;

var builder = WebApplication.CreateBuilder(args);
const string ConnectionString = "AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;DefaultEndpointsProtocol=http;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04);

var blobServiceClient = new BlobServiceClient(ConnectionString, options);
builder.Services.AddSingleton(_ => blobServiceClient);


builder.Services.AddSingleton<IBlobStorage, AzureBlobStorage>();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.MapControllers();
app.Urls.Add("http://localhost:5000");
app.Run();

public partial class Program { }
