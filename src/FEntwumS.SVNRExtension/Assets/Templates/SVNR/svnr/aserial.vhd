
library ieee;
use ieee.std_logic_1164.all;
use ieee.math_real.all;
use ieee.numeric_std.all;

-- WS2812 Treiber
-- Die Ansteuerung der WS2812 erfolgt über eine einzelne Datenleitung mit einem asynchronen seriellen Protokoll
-- Eine "0" wird dabei über einen kurzen, eine "1" über einen langen High-Puls definiert. 
-- Jede LED benötigt 24 Datenbits (G8:R8:B8). Die Daten aller LEDs werden seriell direkt hintereinander übertragen.
-- Wenn die Datenleitung für mehr als 50µs auf "low" gehalten wird ("reset code"), werden die Daten in die PWM-Register der LEDs übernommen.
-- Timing:
-- senden einer '1' => 900ns(±150ns) HIGH gefolgt von 350ns(±150ns) LOW
-- senden einer '0' => 350ns(±150ns) LOW gefolgt von 900ns(±150ns) HIGH
-- insgesammt dauert das senden eines Bits also 1250ns
-- Quelle: https://www.mikrocontroller.net/articles/WS2812_Ansteuerung

-- Drei Zeiten sind also für den Treiber interressant  350ns, 900ns und 1250ns
-- als Frequezen enspricht das: 1/350ns = 2857142,9Hz; 1/900ns = 1111111,111Hz; 1/1250ns = 800kHz
-- mit hilfe der berechneten Frequenzen kann ein Prescaler Wert CLKFRQ/FREQxns berechnet werden, der
-- angibt, wie viele Ticks unserers Prozessors den Zeiten ensprechen

-- Bsp: CLK = 12 MHZ
-- 1250ns = 12000000/800 000 = 15 Ticks
-- 900ns  = 12000000/1111111,111 = 10,8 Ticks -> 11 Ticks -> 4*(1/12000000) = 916,6666 ns
-- 350ns  = 12000000/2857142,9   = 4,2 Ticks  -> 4 Ticks  -> 4*(1/12000000) = 333,3333 ns

-- round(real((1/12 000 000)/(1250*10**(-9)

-- 100 Mhz -> 
-- 90
--- 35

entity aserial is
  port (
    reset    : in  std_ulogic;
    clk      : in  std_ulogic;
    wnr      : in  std_ulogic;
    data_in  : in  std_ulogic;           -- Das zu sendende Bit
    data_out : out std_ulogic;           -- Serieller Ausgang WS2812
    run 	 : out std_ulogic  -- '1' => können keine neuen Daten angenommen werden
    );
end aserial;

architecture dut of aserial is
  constant CLKFRQ: integer := 50000000;

  --constant CLKPERIODE           : time    := --(real(1) / real(CLKFRQ) * real(1000000000000)) * 1 ps;
  constant CLK_count_1250ns     : natural := ((((CLKFRQ*10)/800000)+5)/10)-2; --(((CLKFRQ*10)/8000000)+5)/10;--integer(round(real((real(1)/real(CLKFRQ))/real(1250*10**(-9)))));-- 15;--((integer((round(real(CLKFRQ*10)/real(80*(10**2)))))+5)/10)-2; --15; --integer(1250 ns / CLKPERIODE) - 2;
  constant CLK_count_350nS      : natural := ((((CLKFRQ*10)/2857143)+5)/10)-1;--(((CLKFRQ*10)/28571429)+5)/10;--4;--integer(round(real(CLKFRQ)*real(350)*(10**(-9.0))))-1;
  constant CLK_count_900nS      : natural := ((((CLKFRQ*10)/1111111)+5)/10)-1; --(((CLKFRQ*10)/11111111)+5)/10;--9;--integer(round(real(CLKFRQ)*real(900)*(10**(-9.0))))-1;
  
  signal vcount                                        : natural range 0 to CLK_count_1250ns;
  signal vrun                                          : std_ulogic := '0';
  signal vCLK_count_curr                               : natural range 0 to CLK_count_1250ns;
  
  signal test			: natural := CLK_count_1250ns;
  signal test350			: natural := CLK_count_350nS;
  signal test900			: natural := CLK_count_900nS;
begin

-- ALternativ vorschlag:
  -- Ein Zähler
  clkcnt_p : process (clk) is
  begin  -- process clkcnt_p
    if rising_edge(clk) then      -- rising clock edge
      if reset = '0' then               -- synchronous reset (active low)
        vcount <= 0;
      else
        if vcount /= CLK_count_1250ns and vrun = '1' then -- CLK_count_1250ns
          vcount <= vcount + 1;
        else
          vcount <= 0;
        end if;
      end if;
    end if;
  end process clkcnt_p;

  -- Eine Mini- FSM
  minifsm_p : process (clk) is
  begin  -- process minifsm_p
    if rising_edge(clk) then            -- rising clock edge
      if reset = '0' then               -- synchronous reset (active low)
        vrun <= '0';
      else
        if vrun = '0' then              -- Zustand "IDLE"
          if wnr = '1' then
            vrun <= '1';
          end if;
        else                            -- vrun = '1' : ZUSTAND Running
          if vcount = CLK_count_1250ns then --CLK_count_1250ns
            vrun <= '0';
          end if;
        end if;
      end if;
    end if;
  end process minifsm_p;

  -- Kombinatorik (=Mux) für die Länge der High-Zeit
  vCLK_count_curr <= CLK_count_350nS when data_in = '0' else --CLK_count_350nS
                     CLK_count_900nS;	-- CLK_count_900nS
  -- Kombinatorik (=Mux) für das AUsgangssignal
  data_out <= '1' when vrun = '1' and vcount <= vCLK_count_curr else '0';

  -- Kombinatorik
  run <= vrun;

  
end dut;
