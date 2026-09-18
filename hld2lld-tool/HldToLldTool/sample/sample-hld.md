# HLD: Online Food Ordering Platform

## Overview
A platform that lets customers browse restaurants, place orders, pay online,
and track delivery in real time. Restaurants manage their menu and incoming
orders through a partner dashboard. Delivery partners receive assignments via
a mobile app.

## High-Level Components
- **Customer Web/Mobile App** - browsing, cart, checkout, order tracking
- **Restaurant Partner Dashboard** - menu management, order acceptance
- **Delivery Partner App** - order pickup/delivery, live location updates
- **Order Service** - owns the order lifecycle (created, confirmed, preparing,
  out for delivery, delivered, cancelled)
- **Payment Service** - integrates with a third-party payment gateway,
  handles refunds
- **Notification Service** - push/SMS/email notifications to customers,
  restaurants, and delivery partners
- **Search & Catalog Service** - restaurant and menu search/filtering
- **Delivery Matching Service** - assigns the nearest available delivery
  partner to a confirmed order

## Key Flows
1. Customer searches restaurants, adds items to cart, checks out.
2. Order Service creates the order and requests payment authorization.
3. On successful payment, the order is confirmed and sent to the restaurant.
4. Restaurant accepts and starts preparing; Delivery Matching Service assigns
   a delivery partner.
5. Delivery partner picks up the order and delivers it; customer tracks
   status in real time.

## Non-Functional Requirements
- Must handle regional traffic spikes during meal times (3-5x baseline).
- Payment must be PCI-DSS compliant (no card data stored by us).
- Order status updates should reach the customer within 2 seconds.
- 99.9% availability target for the ordering path.

## Assumed Tech Direction
- Deployed on AWS.
- Microservices communicating over REST + an event bus for async updates
  (order status changes, delivery location pings).
