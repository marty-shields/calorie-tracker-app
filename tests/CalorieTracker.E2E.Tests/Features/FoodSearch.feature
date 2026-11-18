Feature: Food search
  The Add sheet's Search foods opens food search over the bundled foods, always to add to a meal. A tap on a result opens Serving, never a values screen. An old link without a meal adds to the meal of the time of day. Results update as I type. On the first launch the foods may still be loading; the screen says so.

  @smoke
  Scenario: Search foods opens from the Add sheet
    Given I am on the Dashboard
    When I open Foods from the Add sheet
    Then I am on search to add to a meal

  Scenario: Nothing typed shows the prompt
    Given I have opened Foods
    Then Foods shows "Search for a food"

  Scenario: Typing lists matching foods with their energy
    Given I have opened Foods
    When I search for "oat"
    Then the results include a food named like "oat"
    And every result is a food with its energy per 100 g or ml

  Scenario: Foods shows the search box and results only
    Given I have opened Foods
    And I have searched for "milk"
    Then no type chips show
    And the results include a food named like "milk"

  Scenario: A search nothing matches says so
    Given I have opened Foods
    When I search for "zqxjvkw"
    Then Foods shows "No foods match"

  Scenario: Android back from the results returns to the Dashboard
    Given I have opened Foods
    When I press Android back
    Then I am on the Dashboard

  Scenario: The Dashboard item returns to the Dashboard
    Given I have opened Foods
    When I go back to the Dashboard
    Then I am on the Dashboard

  Scenario: An old link with a type still opens search with the query typed
    When I open the old link "/foods?q=oat&type=Drink"
    Then "oat" is still typed
    And the results include a food named like "oat"

  Scenario: An old link without a meal adds to the meal of the time of day
    When I open the old link "/foods"
    Then search is titled for a meal of the time of day

  Scenario: A tapped result opens Serving and never a values screen
    Given I have opened Foods
    And I have searched for "oat"
    When I open the first result
    Then Serving shows the food's name and an amount in grams
    And no food values screen is shown

  @smoke
  Scenario: The barcode button in search opens Scan for the same meal and Back returns to the search
    Given I am on the Dashboard
    When I tap + on Dinner
    And I search for "oat"
    And I tap the barcode button in search
    And I refuse the camera permission
    Then I am on Scan
    And Scan says it is adding to Dinner today
    When I go back with the back arrow
    Then search is titled "Add to Dinner" with no navigation bar
    And "oat" is still typed
