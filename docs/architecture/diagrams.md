# Architecture Diagrams — Koras.Results

## 1. Package dependency diagram

```mermaid
graph TD
    subgraph Satellites
        ASP[Koras.Results.AspNetCore]
        FV[Koras.Results.FluentValidation]
        MED[Koras.Results.MediatR]
        OTEL[Koras.Results.OpenTelemetry]
    end
    CORE[Koras.Results<br/><i>zero dependencies</i>]

    ASP -->|ProjectReference| CORE
    FV --> CORE
    MED --> FV
    MED --> CORE
    OTEL --> CORE

    ASP -.->|FrameworkReference| NETCORE[Microsoft.AspNetCore.App]
    FV -.->|NuGet| FVPKG[FluentValidation ≥11.9]
    MED -.->|NuGet| MEDPKG["MediatR [12.4, 13)"]
    OTEL -.->|NuGet| DS[System.Diagnostics.DiagnosticSource]
```

## 2. Component architecture

```mermaid
graph LR
    subgraph "Koras.Results (core)"
        R["Result / Result&lt;T&gt;<br/>readonly structs"]
        E["Error model<br/>Error / ValidationError / AggregateError"]
        X["Composition<br/>Map · Bind · Match · Ensure · Tap<br/>Try · Combine · async variants"]
        S["STJ converters"]
        R --- E
        X --- R
        S --- R
        S --- E
    end
    subgraph "Koras.Results.AspNetCore"
        MAP["ErrorTypeStatusMap"]
        PD["ProblemDetails builder"]
        MIN["IResult adapters (Minimal API)"]
        MVC["IActionResult adapters (MVC)"]
        OPT["KorasResultsOptions + DI"]
        LOC["IErrorMessageLocalizer"]
        PD --> MAP
        PD --> LOC
        MIN --> PD
        MVC --> PD
        OPT --> MAP
    end
    MIN --> R
    MVC --> R
    PD --> E
```

## 3. Request lifecycle (Minimal API)

```mermaid
sequenceDiagram
    participant C as Client
    participant EP as Endpoint
    participant V as Validator (FluentValidation)
    participant D as Domain service
    participant M as ResultHttpMapper

    C->>EP: POST /orders
    EP->>V: ValidateToResultAsync(cmd, ct)
    alt invalid
        V-->>EP: Result failure (ValidationError)
        EP->>M: ToHttpResult()
        M-->>C: 400 application/problem+json (errors dictionary)
    else valid
        EP->>D: PlaceOrder(cmd) : Result<Order>
        alt domain failure
            D-->>EP: Failure (e.g. Conflict "Order.InsufficientStock")
            EP->>M: ToHttpResult()
            M-->>C: 409 application/problem+json (errorCode)
        else success
            D-->>EP: Success(order)
            EP->>M: ToHttpResult(o => Created(...))
            M-->>C: 201 + body
        end
    end
```

## 4. Error lifecycle

```mermaid
stateDiagram-v2
    [*] --> Created: Error factory / Try mapper / validator
    Created --> Carried: Result.Failure(error)
    Carried --> Propagated: Bind/Map short-circuit (identity preserved)
    Propagated --> Aggregated: Result.Combine (multi-failure)
    Propagated --> Projected
    Aggregated --> Projected
    Projected --> HTTP: ProblemDetails (AspNetCore)
    Projected --> Trace: Activity tags (OpenTelemetry)
    Projected --> Handled: Match / TapError / TryGetValue
    HTTP --> [*]
    Trace --> [*]
    Handled --> [*]
```

## 5. Dependency-injection flow (AspNetCore)

```mermaid
flowchart TD
    A["services.AddKorasResults(configure)"] --> B["Configure&lt;KorasResultsOptions&gt;"]
    B --> C["ValidateOnStart: status codes in range,<br/>factories non-null"]
    A --> D["TryAddSingleton&lt;IErrorMessageLocalizer,<br/>PassThroughLocalizer&gt;"]
    A --> E["TryAddSingleton&lt;ResultHttpMapper&gt;"]
    E --> F["Endpoint / controller calls<br/>ToHttpResult / ToActionResult"]
    F --> G{Options resolved<br/>via IOptions}
    G --> H[ProblemDetails response]
```

## 6. Provider lifecycle

Not applicable in the classic sense — Koras.Results has no I/O providers. The nearest analogue is the projection pipeline lifecycle:

```mermaid
flowchart LR
    REG[DI registration<br/>singleton mapper + options] --> RES[Per-call resolution<br/>IOptions snapshot]
    RES --> USE[Stateless projection<br/>pure function of Error + Options]
    USE --> RES
```

All services are stateless singletons; there is no per-request state, no disposal, no warm-up.

## 7. Telemetry flow

See [observability.md](observability.md#telemetry-flow) for the telemetry flow diagram.
