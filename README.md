# EasyORM
Simple ORM for SQL Server

The easiest thing is to get a free azure account and point it to a SQL Server database to test.
Limitations: It does not currently support relating objects. My goal was simplicity and overhead from the recurring reflection and the additional queries for related objects was something I didnt want to add.

This was a learning experience so PR's are welcome.

## First Steps:
1. The ORM has a public property called ConnectionString that needs to be set before you can interact with the database. 
```
EasyORM.ORM.ConnectionString = "my connection string";
```

2. It also assumes your table name will match your "model" name.

### Create a model
GetModelFromTable(string tableName) Use this to get the model from an existing database table. You can copy this into a public class for a model. This is useful in a debug setting where you can set breakpoint and have the code tell you what your model should look like.
For example, you could run the following code

```
var model = EasyORM.ORM.Model.GetModelFromTable("Person");
```
That would return a string like:
```
    public int ID { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
```
You could use that to create your model.

```
public class Person
{
    public int ID { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

### Create object and save to a database
InsertAsync(T currentObject)
Once you create an object and populate the properties you can use the Insert method to write the data to the database.
```
var p = new Person();
p.FirstName = "Jerry";
p.LastName = "Seinfield";

await EasyORM.ORM.Actions<Person>.InsertAsync(p);
```
### Update object and save the update to the database
UpdateAsync(T currentObject)
Once you have queried an object from the database, you can update any of the properties and update the database record by using the Update method

```
p.LastName = "Constanza";
await EasyORM.ORM.Actions<Person>.UpdateAsync(p);
```

### Deleting object and data from the database
DeleteAsync(T currentObject)
You can delete an object from the database by passing the object into the Delete method.
```
await EasyORM.ORM.Actions<Person>.DeleteAsync(p);
```

### Query object by ID
T GetObjectByIDAsync(int ID)
Once you have created an object and saved it to the database, you can query that object by the ID

```
var p = await EasyORM.ORM.Query<Person>.GetObjectByIDAsync(1);
```
### Querying Data

Currently you can query the database by creating a queryset object and adding parameter objects to it. This allows multiple criteria. Once you have added all the criteria, you pass it into the query method This returns data in a list of T. Both options have an optional cancellation token parameter.

### Task<List<T>> RunQuerySetAsync(QuerySet qs, CancellationToken cancellationToken = default(CancellationToken))
```
var qs = new EasyORM.QuerySet();
qs.AddParameter("LastName", "like", "Seinfield");

var results = await EasyORM.ORM.Query<Person>.RunQuerySetAsync(qs);
```

You also have the option of passing the SQL into the query directly. I like this for more complicated queries since I actually like writing SQL.  I usually create a model to represent the results and it works well for that. 

### Task<List<T>> RunSQLAsync(string sql, CancellationToken cancellationToken = default(CancellationToken))
DO NOT CONCATENATE SQL! If you have parameters use the RunParamSQLAsync method below.

```
var results = await EasyORM.ORM.Query<Person>.RunSQLAsync("select * from Person");
```

### New in version 1.0.1

### Task<List<T>> RunParamSQLAsync(string sql, CancellationToken cancellationToken = default(CancellationToken))

This allows you send SQL to the ORM and it will parameterize it for you.

```
string lastName = "Seinfeld";

var sql = $"select* from Person p where p.LastName = [lastName|{lastName}]";
// this syntax in the brackets gives your variable a parameter name named lastName with the value of the variable.

var results = await EasyORM.ORM.Query<Person>.RunParamSQLAsync(sql);
```

