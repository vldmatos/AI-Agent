using AI_Agent.Components;
using AI_Agent.Services;
using AI_Agent.Services.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI;
using System.ClientModel;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var credential = new ApiKeyCredential(builder.Configuration["GitHubModels:Token"] ?? throw new InvalidOperationException("Missing configuration: GitHubModels:Token. See the README for details."));
var openAIOptions = new OpenAIClientOptions()
{
    Endpoint = new Uri("https://models.inference.ai.azure.com")
};

var ghModelsClient = new OpenAIClient(credential, openAIOptions);
var chatClient = ghModelsClient.AsChatClient("gpt-4o-mini");
var embeddingGenerator = ghModelsClient.AsEmbeddingGenerator("text-embedding-3-small");

var vectorStore = new JsonVectorStore(Path.Combine(AppContext.BaseDirectory, "vector-store"));

builder.Services.AddSingleton<IVectorStore>(vectorStore);
builder.Services.AddScoped<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

builder.Services.AddDbContext<IngestionCacheDbContext>(options =>
    options.UseSqlite("Data Source=ingestioncache.db"));

var application = builder.Build();
IngestionCacheDbContext.Initialize(application.Services);

if (!application.Environment.IsDevelopment())
{
    application.UseExceptionHandler("/Error", createScopeForErrors: true);
    application.UseHsts();
}

application.UseHttpsRedirection();
application.UseAntiforgery();

application.UseStaticFiles();
application.MapRazorComponents<App>()
           .AddInteractiveServerRenderMode();

await DataIngestor.IngestDataAsync(application.Services,
    new PDFDirectorySource(Path.Combine(builder.Environment.WebRootPath, "Data")));

application.Run();
