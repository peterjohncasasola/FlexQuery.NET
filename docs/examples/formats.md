# Query Format Examples

FlexQuery.NET natively supports multiple formats to handle both simple URL requests and complex JSON objects sent from data-grid UI builders.

## DSL Format with Range + Set

Customers aged 25–40 in specific statuses using the compact Domain Specific Language.

### Request
```http
GET /api/customers
  ?filter=(age:between:25,40)%26(status:in:Active,Review)
  &sort=age:asc
```

---

## JSON Format Filter (Complex OR/AND Nesting)

Active customers in London OR any pending customers. This is best sent as a URL-Encoded JSON string.

### Request
```http
GET /api/customers
  ?filter={"logic":"or","filters":[{"logic":"and","filters":[{"field":"city","operator":"eq","value":"London"},{"field":"status","operator":"eq","value":"Active"}]},{"field":"status","operator":"eq","value":"Pending"}]}
```

### Decoded JSON Payload
```json
{
  "logic": "or",
  "filters": [
    {
      "logic": "and",
      "filters": [
        { "field": "city",   "operator": "eq", "value": "London" },
        { "field": "status", "operator": "eq", "value": "Active" }
      ]
    },
    { "field": "status", "operator": "eq", "value": "Pending" }
  ]
}
```

---

## Indexed Format (Form Submission)

Filter by multiple fields using standard HTML array-style parameters (often default in jQuery or basic form submissions).

### Request
```http
GET /api/customers
  ?filter[0].field=city&filter[0].operator=eq&filter[0].value=London
  &filter[1].field=status&filter[1].operator=eq&filter[1].value=Active
  &logic=and
```
