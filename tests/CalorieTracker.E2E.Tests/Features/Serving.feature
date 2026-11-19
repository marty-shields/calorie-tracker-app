Feature: Serving
  The Serving screen adds a food to a meal. A preset meal comes from a meal's + on Today, then search and a result; a food with a serving needs a scanned product, so the Grams and Servings switch is covered by bUnit, not here.

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

  Scenario: Add saves the entry and returns to Today
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

  Scenario: Add an own serving to a food
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    When I add my own serving "1 bowl" of "40"
    Then Serving shows the serving "1 bowl"
    And Serving has the Grams and Servings switch
    And the weight reads "= 40 g"

  Scenario: Choose between two servings
    Given I have opened Serving for the first food found by "oat" with Lunch preset
    And I have added my own serving "1 bowl" of "40"
    And I have added my own serving "1 cup" of "90"
    Then the servings list reads "1 bowl, 1 cup"
    When I choose the serving "1 bowl"
    And I enter an amount of "2"
    Then the weight reads "= 80 g"
    When I tap Add
    Then I am on the Dashboard
    And the Lunch entry reads "2 servings · 1 bowl"
