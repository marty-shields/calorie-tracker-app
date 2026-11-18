Feature: Food search
  The Add sheet's Search foods opens food search over the bundled foods, for a meal. Without a meal, search is reached only by an old link (the scenarios below that open Foods use it) until that view is removed. Results update as I type. On the first launch the foods may still be loading; the screen says so.

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

  Scenario: Tapping a result shows its values and Back to results returns
    Given I have opened Foods
    And I have searched for "oat"
    When I open the first result
    Then the food's name and its energy are shown
    When I tap Back to results
    Then the results show again with "oat" still typed

  Scenario: Android back from a food's values returns to the results
    Given I have opened Foods
    And I have searched for "oat"
    And I have opened the first result
    When I press Android back
    Then the results show again with "oat" still typed

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
