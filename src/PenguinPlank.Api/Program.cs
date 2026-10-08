using PenguinPlank.Api.Composition;
using PenguinPlank.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// The composition root. Application boundary interfaces are bound to their
// Infrastructure implementations here (and only here); later Phase A tasks fill
// this in. Wiring it now proves the single dependency-injection entry point.
builder.Services.AddPenguinPlankServices(builder.Configuration);

// Registers the one-time, idempotent Owner bootstrap (requirement A2 §5.8). The service and
// its operator-supplied options (email + initial password) are bound here; the actual guarded
// owner-only invocation endpoint is wired in task 9.3. There is no default production password
// and no anonymous endpoint that lets anyone create an Owner.
builder.Services.AddOwnerBootstrap(builder.Configuration);

// Registers the RFC 7807 ProblemDetails error surface (requirement A1 §4.8, A6 §8.5): the
// ProblemDetails service with a correlation-id + stable-code customization, and the
// unhandled-exception handler that turns an unexpected exception into a generic 500 with no
// leaked detail. The matching middleware is added to the pipeline below.
builder.Services.AddPenguinPlankProblemDetails();

// The HTTP pipeline is populated by later Phase A tasks. This skeleton only
// establishes the host so the solution builds and the wiring can be verified.
builder.Services.AddOpenApi();

var app = builder.Build();

// First in the pipeline so it catches exceptions thrown by everything that follows and renders
// them as a safe ProblemDetails (requirement A1 §4.8, A6 §8.5).
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Authentication must run before authorization so the fallback authenticated-user policy
// (registered in AddPenguinPlankAuthorization) can challenge with a registered scheme.
app.UseAuthentication();
app.UseAuthorization();

// Publish the authenticated actor onto the scoped IActorContextAccessor so the audit SaveChanges
// interceptor can attribute a business mutation to its actor (requirements A2 §5.12, A4 §6.7).
// Runs after authentication so the principal is resolved; anonymous requests set nothing (the only
// anonymous mutation, the Owner bootstrap, records the system actor at its own boundary).
app.UseMiddleware<PenguinPlank.Api.Endpoints.ActorContextMiddleware>();

// CSRF protection on mutations (requirement A5 §7.2). UseAntiforgery runs after authentication and
// authorization and before the endpoints so a mutating request is validated against the token the
// browser obtained from GET /api/v1/auth/csrf. It auto-validates form-bound endpoints; the JSON
// admin/auth mutations validate the token explicitly in their handlers (AntiforgeryGuard).
app.UseAntiforgery();

// The thin /api/v1 catalog endpoints (task 9.1). Each resolves the authenticated actor at the
// boundary, maps the versioned Contracts DTOs to Application requests, invokes the catalog
// reader/writer/care-profile store, and maps the typed Result/read model back to HTTP — with
// paging/filter/stable-sort/total-count on lists, Idempotency-Key on mutations, and ETag/If-Match
// on edits. All run under the StaffOrOwner policy so an unauthenticated caller is challenged.
app.MapCatalogEndpoints();

// The thin /api/v1/media endpoints (task 9.2): validated upload via IFileStore, authorized
// download, and metadata read/edit. All run under the StaffOrOwner policy so an unauthenticated
// caller is challenged — a public-approved asset is never anonymously downloadable (requirement
// 6.11).
app.MapMediaEndpoints();

// The thin Owner-only administration endpoints (task 9.3): /settings, /users, /audit under the
// OwnerOnly policy, plus the one-time Owner bootstrap mapped anonymously (first run has no Owner).
// Each resolves the authenticated actor at the boundary and maps the use-case Result/read model to
// HTTP; the settings edit enforces If-Match optimistic concurrency.
app.MapAdministrationEndpoints();

// The thin authentication/session endpoints (task 9.3): login, logout, current-actor, and the CSRF
// token. Login/logout depend only on the IStaffAuthenticator abstraction so a future mobile client
// can use OIDC+PKCE behind the same contract; the Phase A implementation is the secure HttpOnly
// same-origin cookie, with no bearer token in browser storage.
app.MapAuthEndpoints();

app.Run();

// The host uses top-level statements, so the compiler generates an INTERNAL Program class. The
// in-process acceptance tests (task 9.6) start the API with WebApplicationFactory<Program>, which
// requires the entry-point type to be accessible from the test assembly. This one-line partial
// declaration promotes the generated class to public purely as a test-accessibility shim; it adds
// no behavior and no production surface.
public partial class Program;
