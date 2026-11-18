Feature: Add a food from food search
  A result in search always opens Serving. A meal's + on Today opens search to add to that meal. Each scenario adds one entry and checks where the person lands and the snackbar.

  Scenario: A meal's search adds to that meal and goes to the Dashboard with Undo
    Given I am on the Dashboard
    When I tap + on Lunch
    Then search is titled "Add to Lunch" with no navigation bar
    When I search for "oat"
    And I open the first result
    Then Serving has the Lunch meal chosen
    And Serving does not say a meal was suggested
    When I tap Back on Serving
    Then search is titled "Add to Lunch" with no navigation bar
    And "oat" is still typed
    When I open the first result
    Then Serving has the Lunch meal chosen
    When I tap Add
    Then I am on the Dashboard
    And the snackbar says it was added to Lunch with the action "Undo"
