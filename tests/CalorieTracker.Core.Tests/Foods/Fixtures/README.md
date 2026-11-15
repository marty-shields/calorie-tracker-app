# Open Food Facts fixtures

Saved responses of `GET https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=product_name,brands,nutriments,product_quantity_unit,serving_quantity_unit,categories_tags`, captured 2026-11-14 with one request each (the solid and drink files again 2026-11-15, after `categories_tags` was added to the request). Tests never call the live service.

| File | Barcode | Notes |
| --- | --- | --- |
| solid-3017620422003.json | 3017620422003 | Nutella: grams, two brands, no `en:beverages` category (Food) |
| drink-5449000000996.json | 5449000000996 | Coca-Cola can: `serving_quantity_unit` is `ml` and the categories include `en:beverages` (Drink) |
| missing-0000000000017.json | 0000000000017 | HTTP 200, `status` 0 (no such product) |
| not-food-8410757001090.json | 8410757001090 | HTTP 404, `status` 0 (a product of another type) |
| missing-values-6111246651261.json | 6111246651261 | HTTP 200, `status` 1 (product found) but `product` is empty: no name or nutriments (captured 2026-11-14) |
