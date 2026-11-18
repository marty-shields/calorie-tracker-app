Feature: Search online
  Under the search results, "Search online" sends what was typed to the online food database, only when tapped. No scenario reaches the network: a Debug-build seam answers the search call by the text typed (src/CalorieTracker.App/Scanning/E2EOnlineSearchSeam.cs): "fizzy cola" finds three products of the United Kingdom, "oat" is offline, "milk" is busy and anything else finds nothing. App data is cleared before each scenario, so the first-use notice shows in each.

  Background:
    Given the online search is answered by the test seam

  Scenario: Typing offers the Search online button under the local results, with the first-use notice
    Given I have opened Foods
    When I search for "oat"
    Then Search online is offered as a button under the results
    And the first-use notice reads "This sends what you typed to an online database."
    And no online group shows

  Scenario: A search nothing stored matches offers Search online as the main button, and its products are new
    Given I have opened Foods
    When I search for "fizzy cola"
    Then Foods shows "No foods match"
    And Search online is offered as the main button
    When I tap Search online
    Then the online group is headed "Online · United Kingdom"
    And the online group lists 3 products, each marked NEW
    And the first-use notice is gone

  Scenario: The first-use notice shows once
    Given I have opened Foods
    And I have searched for "oat"
    When I tap Search online
    And I search for "cheddar"
    Then Search online is offered as a button under the results
    And no first-use notice shows

  Scenario: Tapping the can and then the 1 L bottle ends up as one food that offers no servings
    Given I have opened Foods
    And I have searched for "fizzy cola"
    And I have tapped Search online
    When I open the online product "Testco Fizzy Cola" of "330 ml"
    Then the food's name and its energy are shown
    When I tap Back to results
    And I open the online product "Testco Fizzy Cola" of "1 L"
    And I tap Add to diary
    Then Serving opens in ml with no servings offered

  Scenario: The zero-sugar product never merges into the original
    Given I have opened Foods
    And I have searched for "fizzy cola"
    And I have tapped Search online
    When I open the online product "Testco Fizzy Cola" of "330 ml"
    And I tap Back to results
    And I open the online product "Testco Fizzy Cola Zero" of "330 ml"
    And I tap Add to diary
    Then Serving is headed "Testco Fizzy Cola Zero"
    And Serving opens in ml with no servings offered

  Scenario: A product found from a meal's plus opens Serving in ml
    Given I am on the Dashboard
    When I tap + on Dinner
    And I search for "fizzy cola"
    And I tap Search online
    And I open the online product "Testco Fizzy Cola" of "1 L"
    Then Serving has the Dinner meal chosen
    And Serving opens in ml with no servings offered

  Scenario: An own serving added to an online product is the only one offered
    Given I have opened Foods
    And I have searched for "fizzy cola"
    And I have tapped Search online
    When I open the online product "Testco Fizzy Cola" of "330 ml"
    And I tap Add to diary
    And I add my own serving "Glass" of "250"
    Then Serving shows the serving "Glass"
    And Serving has no list of servings
    And the weight reads "= 250 ml"

  Scenario: The online search offline says so and the local results keep working
    Given I have opened Foods
    And I have searched for "oat"
    When I tap Search online
    Then the online search says "You are offline" and offers Try again
    And the local results still show
    When I open the first result
    Then the food's name and its energy are shown

  Scenario: The online search busy says to try again in a minute
    Given I have opened Foods
    And I have searched for "milk"
    When I tap Search online
    Then the online search says "The online search is busy. Try again in a minute." and offers Try again
    And the local results still show

  Scenario: A search with nothing found says so
    Given I have opened Foods
    And I have searched for "cheddar"
    When I tap Search online
    Then the online search says nothing was found
