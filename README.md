TODO(jh) Sorting?
TODO(jh) Benchmarks

Binary operators with precedence:
* `==`, `!=`
* `<=`, `<`, `>=`, `>`
* `&&`
* `||`

Unary operators:
* `!` (Not)
* `-` (Minus)
* `~` (Bitwise not)

Special binary operators:
* `like` (Case insensitive match with `*` wildcards)
* `matches` (Regex match)

## Examples
```csharp
record Checkup(DateTime Date, string Notes);
record User(
    string FirstName,
    string LastName,
    DateTime BirthDate,
    DateTime LastLogin,
    DateTime NextCheckup,
    bool LockedOut,
    List<string> Aliases,
    List<Checkup> PastCheckups);

var now = DateTime.UtcNow;
var users = new List<User>()
{
    new("George", "Kollias", new DateTime(1977, 8, 30), now, now.AddDays(3), false, new() { "gkollias" }, ...),
    new("Karl", "Sanders", new DateTime(1963, 6, 5), now, now.AddDays(30), false, new() { "ksanders" }, ...),
    new("Karl", "Karl", new DateTime(1969, 6, 9), now, now.AddDays(42), false, new() { "karl der große", "kkarl" }, ...),
    ...
};

// Users that are not locked out where the first name matches sand* or the birthday is before 1970/01/01
var filter = QueryCompiler.Compile("!lockedout && (first ~ sand* || birth < 1970/01/01)");
var hits = users.Where(filter);
...

// Nested query operators for IEnumerables
var filter = QueryCompiler.Compile("aliases any { $ like *groß* } || pastcheckups all { Notes == \"\" || date < @now - 3:00:00 }");
...

// Users that logged in in the last 3.5 hours
var filter = QueryCompiler.Compile("@now - lastlogin < 3:30");
...

// Users with checkup in less than 10 days
var filter = QueryCompiler.Compile("nextcheckup - @now < 10:00:00:00");

// Comparison between fields is also supported
var filter = QueryCompiler.Compile("first == last");

// Alternate binary operators are supported to enhance readability in URLs
var filter = QueryCompiler.Compile("first eq last or birth lt 1970/01/01");
...
```
