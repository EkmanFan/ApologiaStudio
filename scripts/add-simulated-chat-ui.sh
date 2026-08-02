#!/usr/bin/env bash

set -Eeuo pipefail

trap 'status=$?
echo
echo "ERROR at line ${LINENO}: ${BASH_COMMAND}"
echo "Exit code: ${status}"
exit "${status}"' ERR

cd "$(dirname "$0")/.."

if [[ ! -f "ApologiaStudio.sln" ]]; then
  echo "ERROR: ApologiaStudio.sln was not found."
  exit 1
fi

echo "Creating directories..."

mkdir -p \
  src/ApologiaStudio.Application/Conversations/CreateConversation \
  src/ApologiaStudio.Infrastructure/InMemory \
  src/ApologiaStudio.Web/Identity \
  tests/ApologiaStudio.UnitTests/Application/Conversations

echo "Creating CreateConversation use case..."

cat > src/ApologiaStudio.Application/Conversations/CreateConversation/CreateConversationCommand.cs <<'EOF'
namespace ApologiaStudio.Application.Conversations.CreateConversation;

public sealed record CreateConversationCommand(
    string Title);
EOF

cat > src/ApologiaStudio.Application/Conversations/CreateConversation/CreateConversationHandler.cs <<'EOF'
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Application.Conversations.CreateConversation;

public sealed class CreateConversationHandler(
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Conversation> HandleAsync(
        CreateConversationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var conversation = Conversation.Create(
            currentUser.UserId,
            command.Title,
            timeProvider.GetUtcNow());

        conversationRepository.Add(conversation);

        await unitOfWork.SaveChangesAsync(
            cancellationToken);

        return conversation;
    }
}
EOF

echo "Creating in-memory infrastructure..."

cat > src/ApologiaStudio.Infrastructure/InMemory/InMemoryConversationRepository.cs <<'EOF'
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Domain.Conversations;

namespace ApologiaStudio.Infrastructure.InMemory;

public sealed class InMemoryConversationRepository
    : IConversationRepository
{
    private readonly Dictionary<ConversationId, Conversation>
        _conversations = [];

    public Task<Conversation?> GetByIdAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _conversations.TryGetValue(
            conversationId,
            out var conversation);

        return Task.FromResult(conversation);
    }

    public void Add(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (!_conversations.TryAdd(
                conversation.Id,
                conversation))
        {
            throw new InvalidOperationException(
                $"Conversation '{conversation.Id}' already exists.");
        }
    }
}
EOF

cat > src/ApologiaStudio.Infrastructure/InMemory/InMemoryUnitOfWork.cs <<'EOF'
using ApologiaStudio.Application.Abstractions.Persistence;

namespace ApologiaStudio.Infrastructure.InMemory;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
EOF

echo "Creating temporary demo identity..."

cat > src/ApologiaStudio.Web/Identity/DemoCurrentUser.cs <<'EOF'
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.Web.Identity;

public sealed class DemoCurrentUser : ICurrentUser
{
    public UserId UserId { get; } = new(
        Guid.Parse(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
}
EOF

echo "Configuring the Web host..."

cat > src/ApologiaStudio.Web/Program.cs <<'EOF'
using ApologiaStudio.AgentRuntime.Execution;
using ApologiaStudio.AgentRuntime.Routing;
using ApologiaStudio.Application.Abstractions.Agents;
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Application.Conversations.SendMessage;
using ApologiaStudio.Infrastructure.InMemory;
using ApologiaStudio.Web.Components;
using ApologiaStudio.Web.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<
    IConversationRepository,
    InMemoryConversationRepository>();

builder.Services.AddScoped<
    IUnitOfWork,
    InMemoryUnitOfWork>();

builder.Services.AddScoped<
    ICurrentUser,
    DemoCurrentUser>();

builder.Services.AddSingleton<TimeProvider>(
    TimeProvider.System);

builder.Services.AddSingleton<
    IAgentRouter,
    DeterministicAgentRouter>();

builder.Services.AddSingleton<
    SimulatedAgentResponseProvider>();

builder.Services.AddSingleton<
    IAgentRuntime,
    SimulatedAgentRuntime>();

builder.Services.AddScoped<
    CreateConversationHandler>();

builder.Services.AddScoped<
    SendMessageHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
EOF

echo "Creating conversation-first home page..."

cat > src/ApologiaStudio.Web/Components/Pages/Home.razor <<'EOF'
@page "/"

@using Microsoft.AspNetCore.Components.Web
@using ApologiaStudio.AgentRuntime.Agents
@using ApologiaStudio.Application.Agents
@using ApologiaStudio.Application.Conversations.CreateConversation
@using ApologiaStudio.Application.Conversations.SendMessage
@using ApologiaStudio.Domain.Agents
@using ApologiaStudio.Domain.Conversations

@rendermode @(new InteractiveServerRenderMode(prerender: false))

@inject CreateConversationHandler CreateConversationHandler
@inject SendMessageHandler SendMessageHandler

<PageTitle>Apologia Studio</PageTitle>

@if (_conversation is null)
{
    <div class="loading">
        Initialisation de l’espace de discussion…
    </div>
}
else
{
    <section class="chat-shell">
        <header class="chat-header">
            <div>
                <h1>Apologia Studio</h1>
                <p>
                    Posez une question. Le spécialiste le plus pertinent
                    sera sélectionné automatiquement.
                </p>
            </div>

            <div class="runtime-badge">
                Runtime simulé
            </div>
        </header>

        <main class="conversation-thread">
            @if (_conversation.Messages.Count == 0 &&
                 string.IsNullOrEmpty(_streamingText))
            {
                <div class="empty-state">
                    <h2>Commencez directement</h2>

                    <p>
                        Exemples :
                    </p>

                    <button type="button"
                            @onclick="UseHistoricalSuggestion">
                        Question historique
                    </button>

                    <button type="button"
                            @onclick="UseApologeticSuggestion">
                        Question apologétique
                    </button>
                </div>
            }

            @foreach (var message in _conversation.Messages)
            {
                <article class="@GetMessageCssClass(message)">
                    <div class="message-author">
                        @GetMessageAuthor(message)
                    </div>

                    <div class="message-content">
                        @message.Content
                    </div>
                </article>
            }

            @if (!string.IsNullOrEmpty(_streamingText))
            {
                <article class="message agent streaming">
                    <div class="message-author">
                        @(_activeAgentName ?? "Agent")
                    </div>

                    <div class="message-content">
                        @_streamingText
                        <span class="cursor">▋</span>
                    </div>
                </article>
            }
        </main>

        @if (!string.IsNullOrWhiteSpace(_routingReason))
        {
            <aside class="routing-information">
                <strong>Routage :</strong>
                @_routingReason
            </aside>
        }

        @if (!string.IsNullOrWhiteSpace(_errorMessage))
        {
            <aside class="error-information">
                @_errorMessage
            </aside>
        }

        <footer class="composer">
            <div class="composer-options">
                <label for="agent-selection">
                    Expert
                </label>

                <select id="agent-selection"
                        @bind="_selectedAgentSlug"
                        disabled="@_isSending">
                    <option value="">
                        Routage automatique
                    </option>

                    <option value="historian">
                        Historien des religions
                    </option>

                    <option value="protestant-apologist">
                        Apologète protestant
                    </option>
                </select>
            </div>

            <textarea @bind="_draft"
                      @bind:event="oninput"
                      disabled="@_isSending"
                      maxlength="50000"
                      rows="4"
                      placeholder="Posez votre question…">
            </textarea>

            <div class="composer-actions">
                <span>
                    @(_isSending
                        ? "Réponse en cours…"
                        : "Le routage peut être automatique ou imposé.")
                </span>

                <button type="button"
                        disabled="@(_isSending ||
                                   string.IsNullOrWhiteSpace(_draft))"
                        @onclick="SendAsync">
                    Envoyer
                </button>
            </div>
        </footer>
    </section>
}

@code {
    private Conversation? _conversation;
    private string _draft = string.Empty;
    private string _selectedAgentSlug = string.Empty;
    private string _streamingText = string.Empty;
    private string? _activeAgentName;
    private string? _routingReason;
    private string? _errorMessage;
    private bool _isSending;

    protected override async Task OnInitializedAsync()
    {
        _conversation =
            await CreateConversationHandler.HandleAsync(
                new CreateConversationCommand(
                    "Nouvelle discussion"),
                CancellationToken.None);
    }

    private void UseSuggestion(string suggestion)
    {
        _draft = suggestion;
    }

    private void UseHistoricalSuggestion()
    {
        UseSuggestion(
            "À quelle époque apparaît historiquement la primauté de l’évêque de Rome ?");
    }

    private void UseApologeticSuggestion()
    {
        UseSuggestion(
            "Comment défendre la résurrection face à une objection athée ?");
    }

    private async Task SendAsync()
    {
        if (_conversation is null ||
            string.IsNullOrWhiteSpace(_draft) ||
            _isSending)
        {
            return;
        }

        var content = _draft.Trim();

        _draft = string.Empty;
        _streamingText = string.Empty;
        _routingReason = null;
        _errorMessage = null;
        _activeAgentName = null;
        _isSending = true;

        try
        {
            var command = new SendMessageCommand(
                _conversation.Id,
                content,
                ResolveRequestedAgentId());

            await foreach (
                var agentEvent in
                SendMessageHandler.HandleAsync(
                    command,
                    CancellationToken.None))
            {
                switch (agentEvent)
                {
                    case AgentSelectedEvent selected:
                        _activeAgentName =
                            selected.AgentName;

                        _routingReason =
                            selected.Reason;

                        break;

                    case TextDeltaEvent delta:
                        _streamingText +=
                            delta.Content;

                        break;

                    case AgentTurnCompletedEvent:
                        _streamingText =
                            string.Empty;

                        break;
                }

                await InvokeAsync(
                    StateHasChanged);
            }
        }
        catch (Exception exception)
        {
            _errorMessage =
                $"La réponse n’a pas pu être produite : " +
                exception.Message;
        }
        finally
        {
            _streamingText = string.Empty;
            _isSending = false;

            await InvokeAsync(
                StateHasChanged);
        }
    }

    private AgentId? ResolveRequestedAgentId()
    {
        return _selectedAgentSlug switch
        {
            "historian" =>
                BuiltInAgents.Historian.Id,

            "protestant-apologist" =>
                BuiltInAgents.ProtestantApologist.Id,

            _ => null
        };
    }

    private static string GetMessageCssClass(
        ConversationMessage message)
    {
        return message.Role switch
        {
            MessageRole.User =>
                "message user",

            MessageRole.Agent =>
                "message agent",

            _ =>
                "message system"
        };
    }

    private static string GetMessageAuthor(
        ConversationMessage message)
    {
        if (message.Role == MessageRole.User)
        {
            return "Vous";
        }

        if (message.AgentId is { } agentId &&
            BuiltInAgents.TryGet(
                agentId,
                out var agent))
        {
            return agent.DisplayName;
        }

        return "Système";
    }
}
EOF

echo "Creating scoped page styles..."

cat > src/ApologiaStudio.Web/Components/Pages/Home.razor.css <<'EOF'
.chat-shell {
    display: grid;
    grid-template-rows: auto minmax(22rem, 1fr) auto auto;
    gap: 1rem;
    min-height: calc(100vh - 7rem);
    max-width: 70rem;
    margin: 0 auto;
}

.chat-header {
    display: flex;
    align-items: start;
    justify-content: space-between;
    gap: 1rem;
    padding-bottom: 1rem;
    border-bottom: 1px solid #d5d7da;
}

.chat-header h1 {
    margin: 0;
}

.chat-header p {
    margin: 0.4rem 0 0;
    color: #59636e;
}

.runtime-badge {
    flex: 0 0 auto;
    padding: 0.35rem 0.7rem;
    border: 1px solid #c7ccd1;
    border-radius: 999px;
    font-size: 0.8rem;
    color: #59636e;
}

.conversation-thread {
    display: flex;
    flex-direction: column;
    gap: 0.8rem;
    overflow-y: auto;
    padding: 0.25rem;
}

.empty-state {
    margin: auto;
    max-width: 38rem;
    text-align: center;
    color: #59636e;
}

.empty-state button {
    margin: 0.3rem;
    padding: 0.55rem 0.8rem;
    border: 1px solid #bfc5cb;
    border-radius: 0.5rem;
    background: transparent;
}

.message {
    max-width: 80%;
    padding: 0.8rem 1rem;
    border-radius: 0.8rem;
}

.message.user {
    align-self: flex-end;
    background: #e7f0ff;
}

.message.agent {
    align-self: flex-start;
    background: #f1f3f5;
}

.message.system {
    align-self: center;
    background: #fff4d6;
}

.message-author {
    margin-bottom: 0.3rem;
    font-size: 0.78rem;
    font-weight: 700;
    color: #4d5965;
}

.message-content {
    white-space: pre-wrap;
    overflow-wrap: anywhere;
}

.streaming {
    opacity: 0.92;
}

.cursor {
    animation: blink 0.8s steps(1) infinite;
}

.routing-information,
.error-information {
    padding: 0.7rem 0.9rem;
    border-radius: 0.5rem;
    font-size: 0.9rem;
}

.routing-information {
    background: #eef3f8;
}

.error-information {
    background: #ffe4e4;
    color: #8b1a1a;
}

.composer {
    display: grid;
    gap: 0.7rem;
    padding-top: 1rem;
    border-top: 1px solid #d5d7da;
}

.composer-options {
    display: flex;
    align-items: center;
    gap: 0.6rem;
}

.composer-options label {
    font-weight: 600;
}

.composer-options select,
.composer textarea {
    border: 1px solid #adb5bd;
    border-radius: 0.5rem;
    background: var(--bs-body-bg);
    color: var(--bs-body-color);
}

.composer-options select {
    padding: 0.4rem 0.55rem;
}

.composer textarea {
    width: 100%;
    resize: vertical;
    padding: 0.75rem;
}

.composer-actions {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 1rem;
    font-size: 0.85rem;
    color: #59636e;
}

.composer-actions button {
    padding: 0.55rem 1.1rem;
    border: 0;
    border-radius: 0.5rem;
    background: #2457a6;
    color: white;
}

.composer-actions button:disabled {
    opacity: 0.5;
}

.loading {
    padding: 3rem;
    text-align: center;
}

@keyframes blink {
    50% {
        opacity: 0;
    }
}

@media (max-width: 700px) {
    .chat-header {
        flex-direction: column;
    }

    .message {
        max-width: 95%;
    }

    .composer-actions {
        align-items: stretch;
        flex-direction: column;
    }
}
EOF

echo "Creating CreateConversation unit tests..."

cat > tests/ApologiaStudio.UnitTests/Application/Conversations/CreateConversationHandlerTests.cs <<'EOF'
using ApologiaStudio.Application.Abstractions.Conversations;
using ApologiaStudio.Application.Abstractions.Identity;
using ApologiaStudio.Application.Abstractions.Persistence;
using ApologiaStudio.Application.Conversations.CreateConversation;
using ApologiaStudio.Domain.Conversations;
using ApologiaStudio.Domain.Users;

namespace ApologiaStudio.UnitTests.Application.Conversations;

public sealed class CreateConversationHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldCreateAndStoreConversation()
    {
        var userId = UserId.New();
        var repository = new FakeConversationRepository();
        var unitOfWork = new FakeUnitOfWork();

        var now = new DateTimeOffset(
            2026,
            8,
            2,
            12,
            0,
            0,
            TimeSpan.Zero);

        var handler = new CreateConversationHandler(
            repository,
            unitOfWork,
            new FakeCurrentUser(userId),
            new FixedTimeProvider(now));

        var conversation = await handler.HandleAsync(
            new CreateConversationCommand(
                "First discussion"),
            CancellationToken.None);

        Assert.Equal(
            userId,
            conversation.OwnerId);

        Assert.Equal(
            "First discussion",
            conversation.Title);

        Assert.Equal(
            now,
            conversation.CreatedAt);

        Assert.Same(
            conversation,
            repository.StoredConversation);

        Assert.Equal(
            1,
            unitOfWork.SaveCount);
    }

    private sealed class FakeConversationRepository
        : IConversationRepository
    {
        public Conversation? StoredConversation { get; private set; }

        public Task<Conversation?> GetByIdAsync(
            ConversationId conversationId,
            CancellationToken cancellationToken)
        {
            var result =
                StoredConversation?.Id == conversationId
                    ? StoredConversation
                    : null;

            return Task.FromResult(result);
        }

        public void Add(Conversation conversation)
        {
            StoredConversation = conversation;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            SaveCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUser(UserId userId)
        : ICurrentUser
    {
        public UserId UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(
        DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            utcNow;
    }
}
EOF

echo "Formatting solution..."

dotnet format ApologiaStudio.sln --no-restore

echo "Running unit tests..."

dotnet test \
  tests/ApologiaStudio.UnitTests/ApologiaStudio.UnitTests.csproj

echo "Building complete solution..."

dotnet build ApologiaStudio.sln --no-restore

echo
echo "Simulated conversational UI created successfully."
echo "Expected unit-test total: 20."
echo
echo "Run the application with:"
echo "  dotnet run --project src/ApologiaStudio.Web"
