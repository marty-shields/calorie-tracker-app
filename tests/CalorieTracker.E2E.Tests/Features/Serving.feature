Feature: Serving
  The Serving screen adds a food to a meal. Without a preset the scenarios tap through Foods (open a food, Add to diary); a preset meal comes from a meal's + on the Dashboard, then search and a result; a food with a serving needs a scanned product, so the Grams and Servings switch is covered by bUnit, not here.

  Scenario: A food from the CoFID seed is added by grams with no switch
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    Then Serving shows the food's name and an amount in grams
    And Serving has no Grams and Servings switch

  Scenario: The meal chips are in order and the preset meal is chosen
    Given I have opened Serving for the first food found by "oat" with Dinner preset
    Then the meal chips read Breakfast, Lunch, Dinner, Snacks in that order
    And the Dinner meal is chosen
    And Add reads "Add to Dinner"
    And Serving does not say a meal was suggested

  Scenario: Without a preset the meal is suggested from the time
    Given I have opened Serving for the first food found by "oat"
    Then Serving says the meal was suggested from the time
    And Add names the chosen meal

  Scenario: An amount of zero shows the error and turns Add off
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    When I enter an amount of "0"
    Then Serving says "Enter an amount above 0."
    And Add is disabled
    And This adds is hidden

  Scenario: A valid amount shows the energy it adds
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    When I enter an amount of "30"
    Then This adds shows the energy for the amount
    And Add is enabled

  Scenario: Add saves the entry and returns to the Dashboard
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    And I enter an amount of "30"
    When I tap Add
    Then I am on the Dashboard
    And the log shows "added for food"

  Scenario: The back arrow returns to the meal's search
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    When I go back with the back arrow
    Then search is titled "Add to Lunch" with no navigation bar

  Scenario: A food that does not exist says so
    Given I am on Foods
    When I open Serving for a food that does not exist
    Then Serving says "Food not found"
