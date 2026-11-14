# Open Food Facts fixtures

Saved responses of `GET https://world.openfoodfacts.org/api/v2/product/{barcode}?fields=product_name,brands,nutriments,product_quantity_unit,serving_quantity_unit`, captured 2026-11-14 with one request each. Tests never call the live service.

| File | Barcode | Notes |
| --- | --- | --- |
| solid-3017620422003.json | 3017620422003 | Nutella: grams, two brands |
| drink-5449000000996.json | 5449000000996 | Coca-Cola can: `serving_quantity_unit` is `ml` |
| missing-0000000000017.json | 0000000000017 | HTTP 200, `status` 0 (no such product) |
| not-food-8410757001090.json | 8410757001090 | HTTP 404, `status` 0 (a product of another type) |
| missing-values-3017620422003-trimmed.json | 3017620422003 | Hand-edited: the solid response with the `proteins*` keys removed (no real response with missing values was found within the request budget) |
