# C# LINQ-Expression based filter language. **Early prototype**.

Written for easy usage and integration.

It compiles a query string to an `Func<T, bool>` that can be used to filter elements, for
example with LINQ `Where(predicate)`.

The fields you can reference and compare are the properties and fields of the `IEnumerable`
element type.

TODO: The documentation is not complete. More will follow.

## Features
* Binary operators: *, /, %, +, -, <<, >>, <, >, <=, >=, ==, !=, &, ^, |, &&, ||
* Special binary operators for string matching:
    * `=?` matches wildcard pattern
    * `!?` does not match wildcard pattern
    * `=~` matches regular expression
    * `!~` does not match regular expression
* Unary operators: -, !, ~
* Field expansion: you do not have to write the full name of fields you reference as long
  as the prefix is unambiguous.  
  Field access is also case insensitive.
* Nested query operators: you can write sub-queries on collection-members using
  binary-operator-like-functions like `any`, `all` and `count`.


## Examples (TODO)
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

var filter = QueryCompiler.Compile("first == last");

// Users that are not locked out where the first name matches the wildcard-pattern sand*
// or the birthday is before 01/01/1970
filter = QueryCompiler.Compile("!lockedout && (first =? sand* || birth < datetime(\"01/01/1970\")");
var hits = users.Where(filter);
...

// Nested query operators for IEnumerables
filter = QueryCompiler.Compile("aliases any { $ =? *groß* } || pastcheckups all { Notes == \"\" || date < @now - 3:00:00 }");
...

// Users that logged in in the last 3.5 hours
filter = QueryCompiler.Compile("@now - lastlogin < timespan(\"3:30\")");
...

// Users with checkup in less than 10 days
filter = QueryCompiler.Compile("nextcheckup - @now < timespan(\"10:00:00:00\")");

// Alternate binary operators are supported to enhance readability in URLs
filter = QueryCompiler.Compile("first eq last or birth lt 1970/01/01");
...
```

## Future Plans

* Make this EF-Core compatible. Largely it already is, but things like string matching or
  datetime parsing need to be specialized. There should be a package like
  `Bundlor.QueryCompiler.EntityFramework` that enables this.
* Maybe: support sorting, limiting and selecting and more stuff you would find on
  relational query languages like cross-products etc.
* More concise and thorough tests
