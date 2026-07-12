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


