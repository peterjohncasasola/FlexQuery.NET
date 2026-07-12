# Basic Examples

Real-world examples showing common FlexQuery.NET usage patterns.

---

## Example 1: List with Filter and Paging

**Scenario:** An admin panel listing users, searchable by name, filterable by status.

**Request:**
```
GET /api/users?filter=status:eq:active&sort=name:asc&page=1&pageSize=20
```

**Controller:**
```csharp
[HttpGet]
public async Task<IActionResult> GetUsers([FromQuery] FlexQueryParameters parameters)
{
    var result = await _context.Users.FlexQueryAsync<User>(parameters, exec =>
    {
        exec.AllowedFields  = new HashSet<string> { "id", "name", "email", "status", "createdAt" };
        exec.SortableFields = new HashSet<string> { "name", "createdAt" };
        exec.MaxFieldDepth  = 1;
    });

    return Ok(result);
}
```

**Response:**
```json
{
  "totalCount": 42,
  "resultCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1,  "name": "Alice Chen",  "email": "alice@example.com",  "status": "active", "createdAt": "2024-03-15T10:00:00Z" },
    { "id": 2,  "name": "Bob Smith",   "email": "bob@example.com",    "status": "active", "createdAt": "2024-04-01T09:30:00Z" },
    { "id": 5,  "name": "Carol White", "email": "carol@example.com",  "status": "active", "createdAt": "2024-04-20T14:00:00Z" }
  ],
  "nextCursorToken": null
}
```

---

## Example 2: Search with Name Contain

**Request:**
```
GET /api/users?filter=name:contains:ali&sort=name:asc&page=1&pageSize=10
```

**SQL Generated:**
```sql
SELECT Id, Name, Email, Status, CreatedAt
FROM Users
WHERE LOWER(Name) LIKE '%ali%'
ORDER BY Name ASC
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY
```

**Response:**
```json
{
  "totalCount": 3,
  "resultCount": 3,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1,  "name": "Alice Chen",  "status": "active" },
    { "id": 8,  "name": "Ali Hassan",  "status": "active" },
    { "id": 12, "name": "Alicia Park", "status": "inactive" }
  ],
  "nextCursorToken": null
}
```

---

## Example 3: Projected Response

**Scenario:** Mobile client only needs id, name, email — reduce payload size.

**Request:**
```
GET /api/users?filter=status:eq:active&select=id,name,email&page=1&pageSize=50
```

**SQL Generated:**
```sql
SELECT Id, Name, Email
FROM Users
WHERE Status = 'active'
ORDER BY Id
OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
```

**Response:**
```json
{
  "totalCount": 42,
  "resultCount": 42,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice Chen",  "email": "alice@example.com" },
    { "id": 2, "name": "Bob Smith",   "email": "bob@example.com"   }
  ],
  "nextCursorToken": null
}
```

---

## Example 4: Multi-Field Sort

**Request:**
```
GET /api/users?sort=status:asc,name:asc,createdAt:desc&page=1&pageSize=20
```

**SQL Generated:**
```sql
ORDER BY Status ASC, Name ASC, CreatedAt DESC
```

---

## Example 5: FQL Filter

**Request:**
```
GET /api/users?query=(name = "alice" OR name = "bob") AND status = "active"&page=1&pageSize=10
```

**Response:**
```json
{
  "totalCount": 2,
  "resultCount": 2,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false,
  "aggregates": null,
  "data": [
    { "id": 1, "name": "Alice Chen",  "status": "active" },
    { "id": 2, "name": "Bob Smith",   "status": "active" }
  ],
  "nextCursorToken": null
}
```

---

## Example 6: IN List Filter

**Request:**
```
GET /api/users?filter=status:in:active,trial,pending&sort=createdAt:desc&page=1&pageSize=20
```

**SQL Generated:**
```sql
WHERE Status IN ('active', 'trial', 'pending')
ORDER BY CreatedAt DESC
```

---

## Example 7: Date Range Filter

**Request:**
```
GET /api/orders?filter=createdAt:between:2024-01-01,2024-12-31&sort=createdAt:asc
```

**SQL Generated:**
```sql
WHERE CreatedAt BETWEEN '2024-01-01' AND '2024-12-31'
ORDER BY CreatedAt ASC
```

---

## Example 8: Null Check

**Request:**
```
GET /api/users?filter=deletedAt:isnull&sort=name:asc
```

Returns only users that have not been soft-deleted.

**SQL Generated:**
```sql
WHERE DeletedAt IS NULL
ORDER BY Name ASC
```

---

## Example 9: Without Paging Count (Faster)

**Request:**
```
GET /api/users?filter=status:eq:active&page=1&pageSize=20&includeCount=false
```

**Response:**
```json
{
  "totalCount": null,
  "resultCount": null,
  "page": 1,
  "pageSize": 20,
  "aggregates": null,
  "data": [ "..." ],
  "nextCursorToken": null
}
```

No `COUNT(*)` query is issued — faster for high-frequency endpoints where count is not needed.

---

## Example 10: Validation Failure

**Request:**
```
GET /api/users?filter=passwordHash:isnotnull
```

**Response (400):**
```json
{
  "title": "Query validation failed",
  "errors": [
    {
      "field": "passwordHash",
      "code": "FIELD_ACCESS_DENIED",
      "message": "Field 'passwordHash' is explicitly blocked."
    }
  ]
}
```
