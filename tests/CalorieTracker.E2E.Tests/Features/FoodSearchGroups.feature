Feature: Food search groups
  Search shows the person's own foods before they type and ranks them above the database while they type. App data is cleared before each scenario, so each one starts with an empty diary and logs what it needs.

  Scenario: Foods logged before show under Recent on the Foods tab with their last amount
    Given I have logged 35 g of the first food found by "oat" in Lunch
    And I have opened Foods
    Then search lists only the Recent group
    When I open the first food in the Recent group
    And I tap Add to diary
    Then Serving shows an amount of "35" g

  Scenario: Another meal's search shows the food under Recent and opens with the last amount
    Given I have logged 35 g of the first food found by "oat" in Lunch
    When I tap + on Dinner
    Then search lists only the Recent group
    When I open the first food in the Recent group
    Then Serving has the Dinner meal chosen
    And Serving shows an amount of "35" g

  Scenario: The same meal's search shows the food under Usual only
    Given I have logged 35 g of the first food found by "oat" in Lunch
    When I tap + on Lunch
    Then search lists only the Usual group
    When I open the first food in the Usual group
    Then Serving shows an amount of "35" g

  Scenario: Typing shows a logged food under From your diary and nowhere else
    Given I have logged 35 g of the first food found by "oat" in Lunch
    And I have opened Foods
    When I search for "oat"
    Then search lists the groups From your diary and Common, in that order
    And the first food in the From your diary group shows "35" g and its calories
    And no food shows twice

  Scenario: A long group shows 5 foods and Show more reveals the rest
    Given I have opened Foods
    When I search for "milk"
    Then the Common group shows 5 foods and a Show more row
    When I tap Show more on the Common group
    Then the Common group shows the rest of its foods
