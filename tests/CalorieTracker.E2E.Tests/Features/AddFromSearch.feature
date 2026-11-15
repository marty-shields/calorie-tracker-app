Feature: Add a food from food search
  Both routes end at the Serving screen. A meal's + on the Dashboard opens search to add to that meal. The Foods tab route is tapped through: open a food, then Add to diary. Each scenario adds one entry and checks where the person lands and the snackbar.

  Scenario: A meal's search adds to that meal and goes to the Dashboard with Undo
    Given I am on the Dashboard
    When I tap + on Lunch
    And search is titled "Add to Lunch" with no navigation bar
    And I have searched for "oat"
    When I open the first result
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

  Scenario: Add to diary from a food's values suggests a meal and returns to the search with View day
    Given I have opened Foods
    And I have searched for "oat"
    And I have opened the first result
    When I tap Add to diary
    Then Serving says the meal was suggested from the time
    When I tap Add
    Then I am on Foods
    And "oat" is still typed
    And the snackbar says it was added to the suggested meal with the action "View day"
