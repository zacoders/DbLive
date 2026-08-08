

# Table Split Example

**Scenario:** The `dbo.Users` table currently has a `PhoneNumber` column. Business requirements 
change to support multiple phone numbers per user, meaning the data must be moved to a new 
`dbo.UserPhones` table.

Instead of a destructive migration that immediately breaks existing applications, DbLive recommends 
this transition seamlessly:

1. **Internal State:** A temporary trigger is introduced to synchronize data between the 
   legacy `dbo.Users.PhoneNumber` column and the new `dbo.UserPhones` table in real-time.
2. **Old Applications:** Continue using `GetUsers_v1`. The database still fulfills this 
   contract, allowing legacy apps to function without interruption.
3. **New Applications:** Upgrade to `GetUsers_v2` for core user data and use a new 
   `GetUserPhones_v1` procedure to retrieve the list of multiple phone numbers.
4. **Breaking Phase (Cleanup):** Once all old applications are decommissioned, you can 
   safely run breaking migrations to clean up the legacy state:
   - The legacy `GetUsers_v1` procedure is dropped.
   - The temporary migration trigger is removed, as it is no longer needed.
   - The redundant `PhoneNumber` column is dropped from the `dbo.Users` table.

```mermaid
flowchart LR
    classDef class_Red   fill:#ffefdf,stroke:#c94a29
    classDef class_Green fill:#d4ffd4,stroke:#333,stroke-width:2px
    classDef class_Blue  fill:#e6f3ff,stroke:#336699,stroke-width:2px
    classDef class_White fill:#ffffff

    subgraph APPs [Applications]
        OldApp[Old Application]:::class_Red
        NewApp[New Application]:::class_Blue
    end

    subgraph DBService [Database]
        subgraph Interface [Stable Interface]
            GetUsers1["GetUsers_v1"]:::class_Red
            GetUsers2[GetUsers_v2]:::class_Blue
            GetUserPhones[GetUserPhones_v1]:::class_Blue
        end
        class Interface class_Green

        subgraph Internals [Internal Database State]
            UsersTable("dbo.Users table")
            Trigger[["Migration Trigger (Temporary)"]]
            UserPhonesTable[/"dbo.UserPhones table"/]
            UsersTable <--> Trigger
            Trigger <--> UserPhonesTable
        end
        class Internals class_White

        GetUsers1 --> UsersTable
        GetUsers2 --> UsersTable
        GetUserPhones --> UserPhonesTable
    end

    OldApp --> GetUsers1
    NewApp --> GetUsers2
    NewApp --> GetUserPhones
```

The sections below show how **interface versioning** and **internal schema evolution** work in practice.
